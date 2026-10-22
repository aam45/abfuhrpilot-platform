using AbfuhrPilot.Application.Common;
using AbfuhrPilot.Application.Dtos;
using AbfuhrPilot.Domain.Common;
using AbfuhrPilot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AbfuhrPilot.Application.Services;

/// <summary>Upload + retrieval of problem-report photos (validation, storage, tenant scope).</summary>
public class PhotoService
{
    public const long MaxBytes = 8 * 1024 * 1024; // 8 MB

    // Content-type → canonical extension allow-list. Anything else is rejected.
    private static readonly Dictionary<string, string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp",
    };

    private readonly IAppDbContext _db;
    private readonly ICurrentUser _me;
    private readonly IPhotoStorage _storage;

    public PhotoService(IAppDbContext db, ICurrentUser me, IPhotoStorage storage)
    {
        _db = db;
        _me = me;
        _storage = storage;
    }

    public async Task<ProblemPhotoDto> UploadAsync(Stream content, string contentType, string originalFileName,
        long sizeBytes, CancellationToken ct)
    {
        if (!Allowed.TryGetValue(contentType ?? string.Empty, out var ext))
            throw new DomainException("Only JPEG, PNG or WebP images are allowed.");
        if (sizeBytes <= 0 || sizeBytes > MaxBytes)
            throw new DomainException($"The image must be between 1 byte and {MaxBytes / (1024 * 1024)} MB.");

        var key = await _storage.SaveAsync(content, ext, _me.CompanyId, ct);
        var photo = new ProblemPhoto
        {
            CompanyId = _me.CompanyId,
            StorageKey = key,
            ContentType = contentType!,
            FileName = SafeName(originalFileName, ext),
            SizeBytes = sizeBytes,
        };
        _db.ProblemPhotos.Add(photo);
        await _db.SaveChangesAsync(ct);
        return ToDto(photo);
    }

    public async Task<(Stream stream, string contentType, string fileName)> GetContentAsync(Guid id, CancellationToken ct)
    {
        var photo = await Find(id, ct);
        var stream = await _storage.OpenReadAsync(photo.StorageKey, ct);
        return (stream, photo.ContentType, photo.FileName);
    }

    public async Task<ProblemPhotoDto> GetAsync(Guid id, CancellationToken ct) => ToDto(await Find(id, ct));

    private async Task<ProblemPhoto> Find(Guid id, CancellationToken ct) =>
        await _db.ProblemPhotos.FirstOrDefaultAsync(p => p.Id == id && p.CompanyId == _me.CompanyId, ct)
        ?? throw new NotFoundException("Photo not found.");

    /// <summary>Public URL is the streaming endpoint — never a filesystem path.</summary>
    public static ProblemPhotoDto ToDto(ProblemPhoto p) =>
        new(p.Id, $"/api/v1/problem-photos/{p.Id}/content", p.FileName, p.ContentType, p.SizeBytes);

    private static string SafeName(string? original, string ext)
    {
        var name = Path.GetFileNameWithoutExtension(original ?? "foto");
        name = new string(name.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());
        if (string.IsNullOrEmpty(name)) name = "foto";
        return name.Length > 40 ? name[..40] + ext : name + ext;
    }
}
