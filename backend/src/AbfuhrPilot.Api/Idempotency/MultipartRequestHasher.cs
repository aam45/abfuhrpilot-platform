using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace AbfuhrPilot.Api.Idempotency;

/// <summary>
/// Computes a content-aware idempotency hash for <c>multipart/form-data</c>
/// bodies by stream-parsing each section with <see cref="MultipartReader"/> and
/// hashing the section's identity (content-disposition + content-type) plus its
/// actual body bytes — instead of the previous path-level "multipart" marker,
/// which could not tell a same-key retry of the SAME file apart from a same-key
/// upload of a genuinely DIFFERENT file.
///
/// The multipart boundary string itself (which differs per HTTP client attempt
/// even for byte-identical content — it is typically a fresh GUID) is
/// intentionally excluded from the hash, so a legitimate retry of the exact
/// same file still replays cleanly.
///
/// COST NOTE: this hasher fully re-reads the buffered request body — a second
/// complete pass in addition to the model binder's own read — so it is only
/// safe to reuse on size-limited multipart endpoints (<c>problem-photos</c>
/// caps the body via <c>[RequestSizeLimit]</c>). Do not wire it onto an
/// unbounded multipart endpoint without reconsidering that double-read cost.
///
/// Lives in the API layer (not <c>AbfuhrPilot.Infrastructure</c>) because it
/// depends on ASP.NET Core's <c>Microsoft.AspNetCore.WebUtilities</c>, which is
/// only available here without adding a framework reference to the
/// otherwise-plain Infrastructure class library.
/// </summary>
public static class MultipartRequestHasher
{
    /// <summary>
    /// Streams through every section of a multipart body and returns a SHA-256
    /// digest over (content-disposition + content-type + body bytes) for each
    /// section, in order. Never buffers a whole section in memory.
    ///
    /// Never throws for malformed multipart input (missing/invalid boundary, or a
    /// valid boundary with a truncated/garbage body) — it always returns a stable
    /// marker hash instead, so this stays safe to call from a security-sensitive
    /// filter. A malformed request still fails downstream at model binding; it can
    /// never poison a replay because <c>IdempotencyFilter</c> only stores 2xx
    /// responses. <see cref="OperationCanceledException"/> is never swallowed and
    /// always propagates.
    /// </summary>
    public static async Task<byte[]> ComputeAsync(Stream body, string contentType, CancellationToken ct)
    {
        using var sha = SHA256.Create();
        var boundary = GetBoundary(contentType);

        if (boundary is null)
        {
            // Malformed/missing boundary: fall back to a stable marker rather than
            // throwing out of a security-sensitive filter. The request will still
            // fail downstream (model binding) if it truly is not valid multipart.
            var fallback = Encoding.UTF8.GetBytes("multipart-no-boundary");
            sha.TransformBlock(fallback, 0, fallback.Length, null, 0);
            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return sha.Hash!;
        }

        try
        {
            var reader = new MultipartReader(boundary, body);
            var buffer = new byte[8192];

            MultipartSection? section;
            while ((section = await reader.ReadNextSectionAsync(ct)) is not null)
            {
                var identity = Encoding.UTF8.GetBytes($"{section.ContentDisposition}\n{section.ContentType}\n");
                sha.TransformBlock(identity, 0, identity.Length, null, 0);

                int read;
                while ((read = await section.Body.ReadAsync(buffer, ct)) > 0)
                    sha.TransformBlock(buffer, 0, read, null, 0);
            }

            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return sha.Hash!;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            // Valid boundary but a malformed/truncated body: the SHA256 instance
            // above may have already been partially fed and cannot be reused after
            // TransformBlock, so hash the marker with a fresh instance. The request
            // still fails downstream at model binding.
            using var fallbackSha = SHA256.Create();
            var fallback = Encoding.UTF8.GetBytes("multipart-malformed-body");
            fallbackSha.TransformBlock(fallback, 0, fallback.Length, null, 0);
            fallbackSha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return fallbackSha.Hash!;
        }
    }

    private static string? GetBoundary(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType)) return null;
        try
        {
            var parsed = MediaTypeHeaderValue.Parse(contentType);
            var boundary = HeaderUtilities.RemoveQuotes(parsed.Boundary).Value;
            return string.IsNullOrWhiteSpace(boundary) ? null : boundary;
        }
        catch (Exception ex) when (ex is FormatException or InvalidOperationException)
        {
            return null;
        }
    }
}
