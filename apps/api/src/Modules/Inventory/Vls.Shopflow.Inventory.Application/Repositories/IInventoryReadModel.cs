using Vls.Shopflow.Inventory.Application.DataTransferObjects;

namespace Vls.Shopflow.Inventory.Application.Repositories;

public interface IInventoryReadModel
{
    Task<InventoryItemDto?> GetBySkuIdAsync(Guid skuId, CancellationToken ct);

    Task<IReadOnlyList<InventoryItemDto>> GetBySkuIdsAsync(
        IReadOnlyList<Guid> skuIds,
        CancellationToken ct);
}
