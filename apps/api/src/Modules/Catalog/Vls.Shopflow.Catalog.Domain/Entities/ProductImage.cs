using Vls.Shopflow.BuildingBlocks.Domain.Entities;

namespace Vls.Shopflow.Catalog.Domain.Entities;

public sealed class ProductImage : Entity<Guid>
{
    public Guid ProductId { get; private set; }
    public Product Product { get; private set; } = default!;

    /// <summary>Public URL returned to clients (R2 custom domain or local /uploads).</summary>
    public string Url { get; private set; } = default!;

    /// <summary>Object key used to delete from storage (never expose to clients).</summary>
    public string? ObjectKey { get; private set; }

    /// <summary>Local | CloudflareR2 — null on legacy rows.</summary>
    public string? StorageProvider { get; private set; }

    public string? ContentType { get; private set; }
    public long? SizeBytes { get; private set; }

    public int SortOrder { get; private set; }
    public bool IsPrimary { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private ProductImage() { }

    public static ProductImage Create(
        Guid productId,
        string publicUrl,
        string? objectKey,
        int sortOrder,
        bool isPrimary,
        string? storageProvider = null,
        Guid? id = null,
        string? contentType = null,
        long? sizeBytes = null)
    {
        return new ProductImage
        {
            Id = id ?? Guid.NewGuid(),
            ProductId = productId,
            Url = publicUrl,
            ObjectKey = objectKey,
            StorageProvider = storageProvider,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            SortOrder = sortOrder,
            IsPrimary = isPrimary,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    internal void SetPrimary(bool value) => IsPrimary = value;

    /// <summary>
    /// Updates persisted storage metadata after a successful R2 upload (backfill / re-key).
    /// Does not change SortOrder / IsPrimary.
    /// </summary>
    public void MarkMigratedToObjectStorage(
        string publicUrl,
        string objectKey,
        string storageProvider,
        string? contentType,
        long? sizeBytes)
    {
        if (string.IsNullOrWhiteSpace(publicUrl))
            throw new ArgumentException("Public URL is required.", nameof(publicUrl));
        if (string.IsNullOrWhiteSpace(objectKey))
            throw new ArgumentException("Object key is required.", nameof(objectKey));
        if (string.IsNullOrWhiteSpace(storageProvider))
            throw new ArgumentException("Storage provider is required.", nameof(storageProvider));

        Url = publicUrl.Trim();
        ObjectKey = objectKey.Trim().Replace('\\', '/');
        StorageProvider = storageProvider.Trim();
        ContentType = string.IsNullOrWhiteSpace(contentType) ? ContentType : contentType.Trim();
        SizeBytes = sizeBytes ?? SizeBytes;
    }
}
