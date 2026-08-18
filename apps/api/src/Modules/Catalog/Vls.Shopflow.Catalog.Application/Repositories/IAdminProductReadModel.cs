using Vls.Shopflow.Catalog.Application.DataTransferObjects;

namespace Vls.Shopflow.Catalog.Application.Repositories;

public interface IAdminProductReadModel
{
    Task<PagedAdminProductsDto> GetPagedAsync(
        int page,
        int pageSize,
        string sort,
        string? q,
        string? categorySlug,
        Guid? categoryId,
        string status,
        string featured,
        CancellationToken ct = default);
}
