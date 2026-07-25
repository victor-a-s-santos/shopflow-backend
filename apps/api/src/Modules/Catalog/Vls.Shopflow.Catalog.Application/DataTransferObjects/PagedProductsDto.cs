namespace Vls.Shopflow.Catalog.Application.DataTransferObjects;

public sealed record PagedProductsDto(
    IReadOnlyList<ProductDto> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    bool HasNextPage,
    bool HasPreviousPage)
{
    /// <summary>Alias of <see cref="TotalItems"/> for older clients that read <c>total</c>.</summary>
    public int Total => TotalItems;
}
