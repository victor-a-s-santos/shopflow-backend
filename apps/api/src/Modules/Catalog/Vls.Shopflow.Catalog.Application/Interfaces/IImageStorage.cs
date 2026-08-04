namespace Vls.Shopflow.Catalog.Application.Interfaces;

public interface IImageStorage
{
    Task<StoredImage> SaveAsync(
        Guid productId,
        string productSlug,
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken);

    /// <summary>
    /// Best-effort object delete. Failures are logged; missing keys should not throw.
    /// </summary>
    Task TryDeleteAsync(string objectKey, string? storageProvider, CancellationToken cancellationToken);
}

public sealed record StoredImage(
    Guid ImageId,
    string PublicUrl,
    string ObjectKey,
    string StorageProvider,
    string ContentType,
    long SizeBytes);
