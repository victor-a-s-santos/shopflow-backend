namespace Vls.Shopflow.Inventory.Application.DataTransferObjects;

public sealed record AdminInventorySkuCategoryDto(Guid Id, string Name, string Slug);

/// <summary>
/// Compact operational row for Admin Inventory (SKU-centric, not product-centric).
/// </summary>
public sealed record AdminInventorySkuListItemDto(
    Guid SkuId,
    Guid ProductId,
    string ProductName,
    string ProductSlug,
    bool ProductIsActive,
    string SkuCode,
    bool SkuIsActive,
    AdminInventorySkuCategoryDto? Category,
    string? PrimaryImageUrl,
    decimal RegularPrice,
    decimal? PromotionalPrice,
    decimal EffectivePrice,
    int PhysicalQuantity,
    int ReservedQuantity,
    int AvailableQuantity,
    string StockStatus,
    string SalesMode,
    int? PackageSize,
    string? PackageLabel,
    string? QuantityUnitLabel,
    DateTimeOffset CreatedAt);

public sealed record PagedAdminInventorySkusDto(
    IReadOnlyList<AdminInventorySkuListItemDto> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    bool HasNextPage,
    bool HasPreviousPage)
{
    public int Total => TotalItems;
}
