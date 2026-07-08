using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.Inventory.Application.Repositories;
using Vls.Shopflow.Inventory.Domain.Entities;

namespace Vls.Shopflow.Inventory.Infrastructure.Repositories;

public sealed class InventoryItemRepository(InventoryDbContext db) : IInventoryItemRepository
{
    public Task<InventoryItem?> GetBySkuIdAsync(Guid skuId, CancellationToken ct)
        => db.InventoryItems
            .Include(i => i.Movements)
            .Include(i => i.Reservations)
            .FirstOrDefaultAsync(i => i.SkuId == skuId, ct);

    public Task<bool> ExistsForSkuAsync(Guid skuId, CancellationToken ct)
        => db.InventoryItems.AnyAsync(i => i.SkuId == skuId, ct);

    public Task AddAsync(InventoryItem item, CancellationToken ct)
        => db.InventoryItems.AddAsync(item, ct).AsTask();
}
