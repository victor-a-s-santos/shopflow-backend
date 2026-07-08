using Vls.Shopflow.Inventory.Application.DataTransferObjects;

namespace Vls.Shopflow.Inventory.Application.Repositories;

public interface IStockMovementReadModel
{
    Task<PagedStockMovementsDto> GetBySkuIdAsync(Guid skuId, int page, int pageSize, CancellationToken ct);
}
