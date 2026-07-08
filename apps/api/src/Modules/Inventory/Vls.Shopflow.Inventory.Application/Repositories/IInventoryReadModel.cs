using Vls.Shopflow.Inventory.Application.DataTransferObjects;

namespace Vls.Shopflow.Inventory.Application.Repositories;

public interface IInventoryReadModel
{
    Task<InventoryItemDto?> GetBySkuIdAsync(Guid skuId, CancellationToken ct);
}
