using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.Catalog.Application.Services.ProductImageR2Backfill;

namespace Vls.Shopflow.Catalog.Infrastructure.Services.Storage;

public sealed class EfProductImageBackfillStore(CatalogDbContext db) : IProductImageBackfillStore
{
    public async Task<IReadOnlyList<ProductImageBackfillRow>> LoadAllAsync(CancellationToken cancellationToken)
    {
        var images = await db.ProductImages
            .AsNoTracking()
            .Include(i => i.Product)
            .ToListAsync(cancellationToken);

        return images
            .Select(i => new ProductImageBackfillRow(
                i.Id,
                i.ProductId,
                i.Product.Slug.Value,
                i.Url,
                i.ObjectKey,
                i.StorageProvider,
                i.ContentType,
                i.SizeBytes))
            .ToList();
    }

    public async Task PersistMigrationAsync(
        Guid imageId,
        string publicUrl,
        string objectKey,
        string storageProvider,
        string contentType,
        long sizeBytes,
        CancellationToken cancellationToken)
    {
        var image = await db.ProductImages.FirstOrDefaultAsync(i => i.Id == imageId, cancellationToken)
                    ?? throw new KeyNotFoundException($"ProductImage {imageId} not found.");

        image.MarkMigratedToObjectStorage(
            publicUrl,
            objectKey,
            storageProvider,
            contentType,
            sizeBytes);

        await db.SaveChangesAsync(cancellationToken);
    }
}
