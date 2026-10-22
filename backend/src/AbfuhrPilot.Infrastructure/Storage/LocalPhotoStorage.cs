using AbfuhrPilot.Application.Common;
using Microsoft.Extensions.Configuration;

namespace AbfuhrPilot.Infrastructure.Storage;

/// <summary>
/// Stores photos on local disk under a configured base directory
/// (<c>Storage:PhotoPath</c>, default <c>App_Data/problem-photos</c>). Storage
/// keys are <c>{companyId}/{guid}{ext}</c> — generated server-side, so no
/// client-supplied path ever reaches the filesystem. Keys are validated to stay
/// inside the base directory (defence against traversal).
/// </summary>
public class LocalPhotoStorage : IPhotoStorage
{
    private readonly string _basePath;

    public LocalPhotoStorage(IConfiguration config)
    {
        var configured = config["Storage:PhotoPath"];
        if (string.IsNullOrWhiteSpace(configured))
            configured = Path.Combine(AppContext.BaseDirectory, "App_Data", "problem-photos");
        _basePath = Path.GetFullPath(configured);
        Directory.CreateDirectory(_basePath);
    }

    public async Task<string> SaveAsync(Stream content, string fileExtension, Guid companyId, CancellationToken ct = default)
    {
        var key = $"{companyId}/{Guid.NewGuid():N}{fileExtension}";
        var full = ResolveInsideBase(key);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await using var file = File.Create(full);
        await content.CopyToAsync(file, ct);
        return key;
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default)
    {
        var full = ResolveInsideBase(storageKey);
        if (!File.Exists(full)) throw new FileNotFoundException("Stored photo missing.", storageKey);
        return Task.FromResult<Stream>(File.OpenRead(full));
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        var full = ResolveInsideBase(storageKey);
        if (File.Exists(full)) File.Delete(full);
        return Task.CompletedTask;
    }

    private string ResolveInsideBase(string key)
    {
        var full = Path.GetFullPath(Path.Combine(_basePath, key));
        if (!full.StartsWith(_basePath, StringComparison.Ordinal))
            throw new InvalidOperationException("Invalid storage key.");
        return full;
    }
}
