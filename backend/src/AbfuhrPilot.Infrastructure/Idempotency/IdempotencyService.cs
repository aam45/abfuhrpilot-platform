using System.Security.Cryptography;
using System.Text;
using AbfuhrPilot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AbfuhrPilot.Infrastructure.Idempotency;

public enum IdempotencyOutcome
{
    /// <summary>No record — execute the request and store the response.</summary>
    FirstRequest = 0,
    /// <summary>Same key + same request hash — return the stored response.</summary>
    Replay = 1,
    /// <summary>Same key but a different request — reject with 409.</summary>
    Conflict = 2,
}

public sealed record IdempotencyLookup(IdempotencyOutcome Outcome, int StatusCode = 0, string ResponseBody = "");

/// <summary>
/// Core idempotency logic (lookup / hash / store), kept out of the MVC filter so
/// it is unit-testable and reusable (e.g. for future message consumers).
/// </summary>
public class IdempotencyService
{
    public static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(24);
    public const int MaxKeyLength = 64;

    private readonly AppDbContext _db;
    private readonly ILogger<IdempotencyService> _logger;

    public IdempotencyService(AppDbContext db, ILogger<IdempotencyService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>SHA-256 over method + path + streamed body. Multipart bodies (file
    /// uploads) are hashed via the optional <paramref name="hashMultipartBody"/>
    /// delegate when supplied — this lets a caller in a layer that can reference
    /// ASP.NET Core's multipart parser (e.g. the API layer's
    /// <c>MultipartRequestHasher</c>) provide a content-aware hash without giving
    /// this Infrastructure class library a framework dependency. When no delegate
    /// is supplied, multipart bodies fall back to a fixed path-level marker (the
    /// original, less precise behavior). The non-multipart body stream is hashed
    /// incrementally (never fully buffered in memory).</summary>
    public static async Task<string> ComputeRequestHashAsync(
        string method, string path, Stream? body, string? contentType, CancellationToken ct = default,
        Func<Stream, string, CancellationToken, Task<byte[]>>? hashMultipartBody = null)
    {
        using var sha = SHA256.Create();
        var prefix = Encoding.UTF8.GetBytes($"{method.ToUpperInvariant()}\n{path}\n");
        sha.TransformBlock(prefix, 0, prefix.Length, null, 0);

        var isMultipart = contentType?.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase) == true;
        if (isMultipart && hashMultipartBody is not null && body is not null)
        {
            var sectionHash = await hashMultipartBody(body, contentType!, ct);
            sha.TransformBlock(sectionHash, 0, sectionHash.Length, null, 0);
        }
        else if (isMultipart)
        {
            var marker = Encoding.UTF8.GetBytes("multipart"); // documented safe first version
            sha.TransformBlock(marker, 0, marker.Length, null, 0);
        }
        else if (body is not null)
        {
            var buffer = new byte[8192];
            int read;
            while ((read = await body.ReadAsync(buffer, ct)) > 0)
                sha.TransformBlock(buffer, 0, read, null, 0);
        }

        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(sha.Hash!);
    }

    public async Task<IdempotencyLookup> LookupAsync(
        Guid companyId, Guid userId, string key, string requestHash, CancellationToken ct)
    {
        var record = await _db.IdempotencyRecords.FirstOrDefaultAsync(r =>
            r.CompanyId == companyId && r.UserId == userId && r.IdempotencyKey == key, ct);

        if (record is null || record.ExpiresAtUtc <= DateTime.UtcNow)
            return new IdempotencyLookup(IdempotencyOutcome.FirstRequest);

        return record.RequestHash == requestHash
            ? new IdempotencyLookup(IdempotencyOutcome.Replay, record.ResponseStatusCode, record.ResponseBody)
            : new IdempotencyLookup(IdempotencyOutcome.Conflict);
    }

    /// <summary>Stores a successful (2xx) response. Failures to store are logged but
    /// never fail the already-successful request (at-least-once semantics).</summary>
    public async Task StoreAsync(Guid companyId, Guid userId, string key, string method, string path,
        string requestHash, int statusCode, string responseBody, CancellationToken ct)
    {
        try
        {
            // Refresh an expired record for the same key instead of violating the unique index.
            var existing = await _db.IdempotencyRecords.FirstOrDefaultAsync(r =>
                r.CompanyId == companyId && r.UserId == userId && r.IdempotencyKey == key, ct);

            var record = existing ?? new IdempotencyRecord { CompanyId = companyId, UserId = userId, IdempotencyKey = key };
            record.RequestMethod = method.ToUpperInvariant();
            record.RequestPath = path;
            record.RequestHash = requestHash;
            record.ResponseStatusCode = statusCode;
            record.ResponseBody = responseBody;
            record.CreatedAtUtc = DateTime.UtcNow;
            record.ExpiresAtUtc = DateTime.UtcNow.Add(DefaultTtl);

            if (existing is null) _db.IdempotencyRecords.Add(record);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Concurrent duplicate insert or transient failure: the response was
            // already produced; a retry will simply re-execute. Log and move on.
            _logger.LogWarning(ex, "Failed to store idempotency record for key {Key} on {Path}.", key, path);
        }
    }
}
