using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.Inventory.Application.DataTransferObjects;
using Vls.Shopflow.Inventory.Application.Repositories;

namespace Vls.Shopflow.Inventory.Infrastructure.Repositories;

public sealed class InventoryReadModel(InventoryDbContext db) : IInventoryReadModel
{
    public async Task<InventoryItemDto?> GetBySkuIdAsync(Guid skuId, CancellationToken ct)
    {
        var item = await db.InventoryItems
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.SkuId == skuId, ct);

        if (item is null)
            return null;

        return new InventoryItemDto(
            item.SkuId,
            item.QuantityOnHand,
            item.QuantityReserved,
            item.QuantityOnHand - item.QuantityReserved);
    }
}
