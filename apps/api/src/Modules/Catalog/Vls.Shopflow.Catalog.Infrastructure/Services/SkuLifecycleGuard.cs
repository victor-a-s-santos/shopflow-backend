using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.Catalog.Application.Interfaces;

namespace Vls.Shopflow.Catalog.Infrastructure.Services;

/// <summary>
/// Reads inventory + orders schemas via raw SQL (same database) without referencing other modules.
/// </summary>
public sealed class SkuLifecycleGuard(CatalogDbContext db) : ISkuLifecycleGuard
{
    public async Task<SkuProtectionStatus> GetProtectionAsync(Guid skuId, CancellationToken cancellationToken)
    {
        var onHandOrReserved = await db.Database
            .SqlQuery<int>($"""
                SELECT CASE
                    WHEN EXISTS (
                        SELECT 1
                        FROM inventory.inventory_items
                        WHERE "SkuId" = {skuId}
                          AND ("QuantityOnHand" > 0 OR "QuantityReserved" > 0)
                    ) THEN 1 ELSE 0
                END AS "Value"
                """)
            .FirstOrDefaultAsync(cancellationToken);

        var movements = await db.Database
            .SqlQuery<int>($"""
                SELECT CASE
                    WHEN EXISTS (
                        SELECT 1
                        FROM inventory.stock_movements
                        WHERE "SkuId" = {skuId}
                    ) THEN 1 ELSE 0
                END AS "Value"
                """)
            .FirstOrDefaultAsync(cancellationToken);

        var orders = await db.Database
            .SqlQuery<int>($"""
                SELECT CASE
                    WHEN EXISTS (
                        SELECT 1
                        FROM orders.order_items
                        WHERE "SkuId" = {skuId}
                    ) THEN 1 ELSE 0
                END AS "Value"
                """)
            .FirstOrDefaultAsync(cancellationToken);

        return new SkuProtectionStatus(
            HasOnHandOrReserved: onHandOrReserved > 0,
            HasMovements: movements > 0,
            HasOrderHistory: orders > 0);
    }
}
