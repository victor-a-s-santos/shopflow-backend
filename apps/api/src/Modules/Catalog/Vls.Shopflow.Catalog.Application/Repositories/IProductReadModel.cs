using Vls.Shopflow.Catalog.Application.DataTransferObjects;

namespace Vls.Shopflow.Catalog.Application.Repositories;

public interface IProductReadModel
{
    Task<ProductDetailedDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ProductDetailedDto?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<PagedProductsDto> GetPagedAsync(
        int page,
        int pageSize,
        string sort,
        string? categorySlug,
        Guid? categoryId,
        string? q,
        CancellationToken ct = default);
}
