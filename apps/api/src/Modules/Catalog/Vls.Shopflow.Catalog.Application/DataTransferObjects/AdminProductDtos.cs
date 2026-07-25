namespace Vls.Shopflow.Catalog.Application.DataTransferObjects;

public sealed record AdminProductCategoryDto(Guid Id, string Name, string Slug);

/// <summary>
/// Compact row for the admin products table (not the public storefront list).
/// </summary>
public sealed record AdminProductListItemDto(
    Guid Id,
    string Name,
    string Slug,
    bool IsActive,
    bool IsFeatured,
    int? DisplayOrder,
    DateTimeOffset CreatedAt,
    AdminProductCategoryDto? Category,
    string? PrimaryImageUrl,
    int SkuCount,
    int ActiveSkuCount,
    decimal? MinPrice,
    bool HasPromotionalPrice);

public sealed record PagedAdminProductsDto(
    IReadOnlyList<AdminProductListItemDto> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    bool HasNextPage,
    bool HasPreviousPage)
{
    /// <summary>Alias of <see cref="TotalItems"/> for clients that read <c>total</c>.</summary>
    public int Total => TotalItems;
}
