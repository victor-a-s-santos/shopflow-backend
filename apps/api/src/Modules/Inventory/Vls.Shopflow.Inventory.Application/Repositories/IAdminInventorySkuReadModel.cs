using Vls.Shopflow.Inventory.Application.DataTransferObjects;

namespace Vls.Shopflow.Inventory.Application.Repositories;

public interface IAdminInventorySkuReadModel
{
    Task<PagedAdminInventorySkusDto> GetPagedAsync(
        int page,
        int pageSize,
        string sort,
        string? q,
        Guid? productId,
        string? categorySlug,
        Guid? categoryId,
        string status,
        string stockStatus,
        CancellationToken ct = default);
}
