namespace Vls.Shopflow.Catalog.Application.DataTransferObjects;

public sealed record ProductDetailedDto(
    Guid Id,
    string Name,
    string Slug,
    bool IsActive,
    Guid? CategoryId,
    string? CategoryName,
    bool HasSkus,
    decimal RegularPrice,
    decimal? PromotionalPrice,
    decimal EffectivePrice,
    IReadOnlyList<SkuDto> Skus,
    IReadOnlyList<ProductImageDto> Images
);