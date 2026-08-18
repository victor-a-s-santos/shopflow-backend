using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vls.Shopflow.Catalog.Application.Interfaces;
using Vls.Shopflow.Catalog.Application.Options;
using Vls.Shopflow.Catalog.Application.Services;

namespace Vls.Shopflow.Catalog.Infrastructure.Services.Storage;

/// <summary>Product-image facade over <see cref="IObjectStorageService"/>.</summary>
public sealed class ProductImageStorageService(
    IObjectStorageService objectStorage,
    IOptions<StorageOptions> storageOptions,
    ILogger<ProductImageStorageService> logger) : IImageStorage
{
    public async Task<StoredImage> SaveAsync(
        Guid productId,
        string productSlug,
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken)
    {
        var imageId = Guid.NewGuid();
        var ext = ProductImageStorageKeys.NormalizeExtension(Path.GetExtension(fileName));
        if (ext == ".bin")
        {
            ext = contentType.ToLowerInvariant() switch
            {
                "image/png" => ".png",
                "image/webp" => ".webp",
                "image/jpeg" or "image/jpg" => ".jpg",
                _ => ".bin"
            };
        }

        var key = ProductImageStorageKeys.Build(
            storageOptions.Value.KeyPrefix,
            productId,
            imageId,
            productSlug,
            ext);

        var uploaded = await objectStorage.UploadAsync(
            new ObjectStorageUploadRequest(
                key,
                content,
                contentType,
                CacheControl: storageOptions.Value.UseCloudflareR2
                    ? R2StorageOptions.ImageCacheControl
                    : null),
            cancellationToken);

        return new StoredImage(
            imageId,
            uploaded.PublicUrl,
            uploaded.ObjectKey,
            objectStorage.ProviderName,
            uploaded.ContentType,
            uploaded.SizeBytes);
    }

    public async Task TryDeleteAsync(string objectKey, string? storageProvider, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
            return;

        try
        {
            if (!string.IsNullOrWhiteSpace(storageProvider)
                && !string.Equals(storageProvider, objectStorage.ProviderName, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(storageProvider, StorageOptions.ProviderLocal, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(storageProvider, StorageOptions.ProviderCloudflareR2, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning(
                    "Skipping delete for unknown storage provider {Provider} key {Key}",
                    storageProvider,
                    objectKey);
                return;
            }

            await objectStorage.DeleteAsync(new ObjectStorageDeleteRequest(objectKey), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Controlled failure deleting product image object {Key}", objectKey);
        }
    }
}
