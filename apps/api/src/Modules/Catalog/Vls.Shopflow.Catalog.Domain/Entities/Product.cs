using Vls.Shopflow.BuildingBlocks.Domain.Entities;
using Vls.Shopflow.BuildingBlocks.Domain.ValueObjects;
using Vls.Shopflow.Catalog.Domain.ValueObjects;

namespace Vls.Shopflow.Catalog.Domain.Entities;

public sealed class Product : Entity<Guid>
{
    private readonly List<Sku> _skus = new();
    private readonly List<ProductImage> _images = new();

    public string Name { get; private set; } = default!;
    public Slug Slug { get; private set; } = default!;
    public Guid? CategoryId { get; private set; }
    public Category? Category { get; private set; }
    public bool IsActive { get; private set; }
    public bool HasSkus { get; private set; }
    public Price BasePrice { get; private set; } = Price.From(0);

    public IReadOnlyCollection<Sku> Skus => _skus.AsReadOnly();
    public IReadOnlyCollection<ProductImage> Images => _images.AsReadOnly();

    private Product() { }

    public static Product CreateWithSkus(string name, Slug slug, Guid? categoryId)
    {
        return new Product
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Slug = slug,
            CategoryId = categoryId,
            IsActive = true,
            HasSkus = true,
            BasePrice = Price.From(0)
        };
    }

    public void AddSku(Sku sku)
    {
        _skus.Add(sku);
    }

    public PriceSummary GetDisplayPrice(DateTimeOffset now)
    {
        if (!HasSkus)
            return new PriceSummary(
                BasePrice.Regular.Amount,
                BasePrice.Promotional?.Amount,
                BasePrice.EffectiveNow(now).Amount);

        var actives = _skus.Where(s => s.IsActive).ToList();
        if (actives.Count == 0)
            return new PriceSummary(0, null, 0);

        var minRegular = actives.Min(s => s.Price.Regular.Amount);
        var promos = actives.Where(s => s.Price.Promotional != null).Select(s => s.Price.Promotional!.Amount).ToList();
        var minPromo = promos.Count > 0 ? promos.Min() : (decimal?)null;
        var effective = minPromo.HasValue ? Math.Min(minRegular, minPromo.Value) : minRegular;
        return new PriceSummary(minRegular, minPromo, effective);
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;

    public void UpdateInfo(string name, Slug slug, Guid? categoryId, bool isActive)
    {
        Name = name?.Trim() ?? Name;
        Slug = slug;
        CategoryId = categoryId;
        IsActive = isActive;
    }

    public Sku? GetSku(Guid skuId) => _skus.FirstOrDefault(s => s.Id == skuId);

    public void RemoveSku(Guid skuId) => _skus.RemoveAll(s => s.Id == skuId);

    public void AddImage(ProductImage image)
    {
        if (image.ProductId != Id)
            throw new InvalidOperationException("Image does not belong to this product.");

        var isFirst = _images.Count == 0;
        if (isFirst || image.IsPrimary)
        {
            foreach (var existing in _images)
                existing.SetPrimary(false);
            image.SetPrimary(true);
        }

        _images.Add(image);
    }

    public void RemoveImage(Guid imageId) => _images.RemoveAll(i => i.Id == imageId);

    public void PromoteImageToPrimary(Guid imageId)
    {
        var target = _images.FirstOrDefault(i => i.Id == imageId)
                     ?? throw new KeyNotFoundException("Image not found.");
        foreach (var existing in _images)
            existing.SetPrimary(false);
        target.SetPrimary(true);
    }
}
