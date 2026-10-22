using AbfuhrPilot.Application.Dtos;
using AbfuhrPilot.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AbfuhrPilot.Api.Controllers;

/// <summary>
/// Upload and retrieval of problem-report photos. Any authenticated user may
/// upload (drivers report problems); content is streamed back only within the
/// caller's company. Uploads are validated (type + size) in the service.
/// </summary>
[ApiController]
[Route("api/v1/problem-photos")]
[Authorize]
public class ProblemPhotosController : ControllerBase
{
    private const long MaxBytes = PhotoService.MaxBytes;
    private readonly PhotoService _photos;
    public ProblemPhotosController(PhotoService photos) => _photos = photos;

    // Idempotent (optional key): multipart bodies are hashed content-aware via
    // MultipartRequestHasher (boundary excluded, section bytes included) — a
    // same-key retry of the SAME file replays the originally stored photo, while
    // a same-key upload of a genuinely different file is rejected as a 409.
    [HttpPost]
    [RequestSizeLimit(MaxBytes + 4096)]
    [Idempotency.Idempotent]
    // NOTE: no [FromForm] on IFormFile — it binds from multipart automatically and
    // the explicit attribute breaks Swashbuckle's OpenAPI generation (known issue).
    public async Task<ProblemPhotoDto> Upload(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            throw new Domain.Common.DomainException("No file provided.");
        await using var stream = file.OpenReadStream();
        return await _photos.UploadAsync(stream, file.ContentType, file.FileName, file.Length, ct);
    }

    [HttpGet("{id:guid}/content")]
    public async Task<IActionResult> Content(Guid id, CancellationToken ct)
    {
        var (stream, contentType, fileName) = await _photos.GetContentAsync(id, ct);
        return File(stream, contentType, fileName);
    }
}
