namespace Vls.Shopflow.Catalog.Application.DataTransferObjects;

public sealed record PagedProductsDto(int Page, int PageSize, int Total, IEnumerable<ProductDto> Items);