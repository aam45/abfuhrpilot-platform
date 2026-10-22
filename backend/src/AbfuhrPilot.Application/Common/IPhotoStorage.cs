namespace AbfuhrPilot.Application.Common;

/// <summary>
/// Blob-storage abstraction for uploaded photos. The default implementation
/// writes to local disk; it can be swapped for S3/Azure Blob/MinIO without
/// touching the application layer. Implementations must generate their own safe
/// keys — never trust a client-supplied filename for the storage path.
/// </summary>
public interface IPhotoStorage
{
    /// <summary>Persists the content and returns an opaque storage key.</summary>
    Task<string> SaveAsync(Stream content, string fileExtension, Guid companyId, CancellationToken ct = default);

    /// <summary>Opens the stored content for reading.</summary>
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default);

    Task DeleteAsync(string storageKey, CancellationToken ct = default);
}
