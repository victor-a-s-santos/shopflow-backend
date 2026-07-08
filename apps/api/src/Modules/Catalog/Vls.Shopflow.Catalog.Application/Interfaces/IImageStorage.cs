namespace Vls.Shopflow.Catalog.Application.Interfaces;

public interface IImageStorage
{
    Task<StoredImage> SaveAsync(
        Guid productId,
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken);
}

public sealed record StoredImage(string Url, string StoragePath);
