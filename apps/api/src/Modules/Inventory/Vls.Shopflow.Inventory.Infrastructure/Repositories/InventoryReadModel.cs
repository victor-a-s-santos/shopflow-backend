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

        return ToDto(item.SkuId, item.QuantityOnHand, item.QuantityReserved);
    }

    public async Task<IReadOnlyList<InventoryItemDto>> GetBySkuIdsAsync(
        IReadOnlyList<Guid> skuIds,
        CancellationToken ct)
    {
        if (skuIds.Count == 0)
            return [];

        var distinctIds = skuIds.Distinct().ToList();

        var items = await db.InventoryItems
            .AsNoTracking()
            .Where(i => distinctIds.Contains(i.SkuId))
            .Select(i => new { i.SkuId, i.QuantityOnHand, i.QuantityReserved })
            .ToListAsync(ct);

        return items
            .Select(i => ToDto(i.SkuId, i.QuantityOnHand, i.QuantityReserved))
            .ToList();
    }

    private static InventoryItemDto ToDto(Guid skuId, int quantityOnHand, int quantityReserved)
        => new(
            skuId,
            quantityOnHand,
            quantityReserved,
            quantityOnHand - quantityReserved);
}
