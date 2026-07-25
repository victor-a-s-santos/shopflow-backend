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

    /// <summary>Storefront highlight — featured products sort first on the public list.</summary>
    public bool IsFeatured { get; private set; }

    /// <summary>Manual storefront order (lower first). Null = after manually ordered products.</summary>
    public int? DisplayOrder { get; private set; }

    /// <summary>Creation timestamp used for novelty sort. Never use UpdatedAt for vitrine order.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<Sku> Skus => _skus.AsReadOnly();
    public IReadOnlyCollection<ProductImage> Images => _images.AsReadOnly();

    private Product() { }

    public static Product CreateWithSkus(
        string name,
        Slug slug,
        Guid? categoryId,
        bool isFeatured = false,
        int? displayOrder = null)
    {
        ValidateDisplayOrder(displayOrder);

        return new Product
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Slug = slug,
            CategoryId = categoryId,
            IsActive = true,
            HasSkus = true,
            BasePrice = Price.From(0),
            IsFeatured = isFeatured,
            DisplayOrder = displayOrder,
            CreatedAt = DateTimeOffset.UtcNow
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

    public void ChangeDisplaySettings(bool isFeatured, int? displayOrder)
    {
        ValidateDisplayOrder(displayOrder);
        IsFeatured = isFeatured;
        DisplayOrder = displayOrder;
    }

    private static void ValidateDisplayOrder(int? displayOrder)
    {
        if (displayOrder is < 0)
            throw new ArgumentOutOfRangeException(nameof(displayOrder), "DisplayOrder cannot be negative.");
    }

    public Sku? GetSku(Guid skuId) => _skus.FirstOrDefault(s => s.Id == skuId);

    public void RemoveSku(Guid skuId) => _skus.RemoveAll(s => s.Id == skuId);

    public const int MaxImages = 10;

    public void AddImage(ProductImage image)
    {
        if (image.ProductId != Id)
            throw new InvalidOperationException("Image does not belong to this product.");

        if (_images.Count >= MaxImages)
            throw new InvalidOperationException($"Product cannot have more than {MaxImages} images.");

        var isFirst = _images.Count == 0;
        if (isFirst || image.IsPrimary)
        {
            foreach (var existing in _images)
                existing.SetPrimary(false);
            image.SetPrimary(true);
        }

        _images.Add(image);
    }

    public void RemoveImage(Guid imageId)
    {
        var removed = _images.FirstOrDefault(i => i.Id == imageId);
        if (removed is null)
            return;

        var wasPrimary = removed.IsPrimary;
        _images.Remove(removed);

        if (wasPrimary && _images.Count > 0)
        {
            var next = _images.OrderBy(i => i.SortOrder).First();
            PromoteImageToPrimary(next.Id);
        }
    }

    public void PromoteImageToPrimary(Guid imageId)
    {
        var target = _images.FirstOrDefault(i => i.Id == imageId)
                     ?? throw new KeyNotFoundException("Image not found.");
        foreach (var existing in _images)
            existing.SetPrimary(false);
        target.SetPrimary(true);
    }
}
