using Vls.Shopflow.BuildingBlocks.Domain.Entities;

namespace Vls.Shopflow.Catalog.Domain.Entities;

public sealed class ProductImage : Entity<Guid>
{
    public Guid ProductId { get; private set; }
    public Product Product { get; private set; } = default!;

    public string Url { get; private set; } = default!;
    public string? StoragePath { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsPrimary { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private ProductImage() { }

    public static ProductImage Create(
        Guid productId,
        string url,
        string? storagePath,
        int sortOrder,
        bool isPrimary)
    {
        return new ProductImage
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            Url = url,
            StoragePath = storagePath,
            SortOrder = sortOrder,
            IsPrimary = isPrimary,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    internal void SetPrimary(bool value) => IsPrimary = value;
}
