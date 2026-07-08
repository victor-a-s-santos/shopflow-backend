using Vls.Shopflow.Inventory.Domain.Entities;

namespace Vls.Shopflow.Inventory.Application.Repositories;

public interface IInventoryItemRepository
{
    Task<InventoryItem?> GetBySkuIdAsync(Guid skuId, CancellationToken ct);
    Task<bool> ExistsForSkuAsync(Guid skuId, CancellationToken ct);
    Task AddAsync(InventoryItem item, CancellationToken ct);
}
