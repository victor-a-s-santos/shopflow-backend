namespace Vls.Shopflow.Catalog.Application.DataTransferObjects;

public sealed record ProductDto(
    Guid Id, 
    string Name, 
    string Slug, 
    bool IsActive, 
    bool HasSkus,
    decimal RegularPrice, 
    decimal? PromotionalPrice, 
    decimal EffectivePrice,
    Guid? CategoryId,
    string? CategoryName,
    string? PrimaryImageUrl);