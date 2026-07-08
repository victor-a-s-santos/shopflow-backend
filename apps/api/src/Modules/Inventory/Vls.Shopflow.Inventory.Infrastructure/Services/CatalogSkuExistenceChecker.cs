using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.Inventory.Application.Interfaces;
using Vls.Shopflow.Inventory.Domain.Exceptions;

namespace Vls.Shopflow.Inventory.Infrastructure.Services;

/// <summary>
/// Reads catalog.product_skus via raw SQL (same database, cross-schema) without referencing Catalog domain types.
/// </summary>
public sealed class CatalogSkuExistenceChecker(InventoryDbContext db) : ISkuExistenceChecker
{
    public async Task<bool> ExistsAsync(Guid skuId, CancellationToken cancellationToken)
    {
        var result = await db.Database
            .SqlQuery<int>($"""
                SELECT COUNT(*)::int AS "Value"
                FROM catalog.product_skus
                WHERE "Id" = {skuId}
                """)
            .FirstOrDefaultAsync(cancellationToken);

        return result > 0;
    }
}
