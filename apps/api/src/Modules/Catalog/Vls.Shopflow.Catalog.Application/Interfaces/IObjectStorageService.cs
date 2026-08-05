namespace Vls.Shopflow.Catalog.Application.Interfaces;

public sealed record ObjectStorageUploadRequest(
    string ObjectKey,
    Stream Content,
    string ContentType,
    string? CacheControl = null);

public sealed record ObjectStorageUploadResult(
    string ObjectKey,
    string PublicUrl,
    string ContentType,
    long SizeBytes);

public sealed record ObjectStorageDeleteRequest(string ObjectKey);

public interface IObjectStorageService
{
    /// <summary>Provider name persisted on ProductImage (Local | CloudflareR2).</summary>
    string ProviderName { get; }

    Task<ObjectStorageUploadResult> UploadAsync(
        ObjectStorageUploadRequest request,
        CancellationToken cancellationToken);

    /// <summary>Deletes the object. Missing keys should not throw.</summary>
    Task DeleteAsync(ObjectStorageDeleteRequest request, CancellationToken cancellationToken);

    /// <summary>Head/exists check for a single object key. Does not list the bucket.</summary>
    Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken);

    string BuildPublicUrl(string objectKey);
}
