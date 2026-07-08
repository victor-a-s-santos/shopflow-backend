using Vls.Shopflow.Catalog.Application.DataTransferObjects;

namespace Vls.Shopflow.Catalog.Application.Repositories;

public interface IProductReadModel
{
    Task<ProductDetailedDto?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<ProductDetailedDto?> GetBySlugAsync(string slug, CancellationToken ct);
    Task<PagedProductsDto> GetPagedAsync(int page, int pageSize, CancellationToken ct);
}