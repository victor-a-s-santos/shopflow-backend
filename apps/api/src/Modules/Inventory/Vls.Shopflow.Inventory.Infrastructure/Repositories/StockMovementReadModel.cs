using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.Inventory.Application.DataTransferObjects;
using Vls.Shopflow.Inventory.Application.Repositories;

namespace Vls.Shopflow.Inventory.Infrastructure.Repositories;

public sealed class StockMovementReadModel(InventoryDbContext db) : IStockMovementReadModel
{
    public async Task<PagedStockMovementsDto> GetBySkuIdAsync(
        Guid skuId,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        var query = db.StockMovements.AsNoTracking().Where(m => m.SkuId == skuId);

        var totalItems = await query.CountAsync(ct);
        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);

        var items = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new StockMovementDto(
                m.Id,
                m.SkuId,
                m.Type.ToString(),
                m.Quantity,
                m.Reason,
                m.CreatedAt))
            .ToListAsync(ct);

        return new PagedStockMovementsDto(page, pageSize, totalItems, totalPages, items);
    }
}
