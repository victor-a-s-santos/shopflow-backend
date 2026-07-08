using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vls.Shopflow.Inventory.Domain.Entities;

namespace Vls.Shopflow.Inventory.Infrastructure.Seed;

public static class DemoClothingInventorySeed
{
    public sealed record SeedResult(int ItemsCreated, int ItemsSkipped);

    public static async Task<SeedResult> SeedAsync(
        InventoryDbContext inventoryDb,
        IReadOnlyList<Guid> skuIds,
        int defaultQuantity,
        string reason,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (skuIds.Count == 0)
        {
            logger.LogWarning("Demo inventory seed: no SKU ids provided.");
            return new SeedResult(0, 0);
        }

        var existingSkuIds = await inventoryDb.InventoryItems
            .AsNoTracking()
            .Where(i => skuIds.Contains(i.SkuId))
            .Select(i => i.SkuId)
            .ToListAsync(cancellationToken);

        var existingSet = existingSkuIds.ToHashSet();
        var created = 0;
        var skipped = 0;

        foreach (var skuId in skuIds)
        {
            if (existingSet.Contains(skuId))
            {
                skipped++;
                continue;
            }

            inventoryDb.InventoryItems.Add(
                InventoryItem.Create(
                    skuId,
                    defaultQuantity,
                    reason,
                    isInitialStock: true));

            created++;
        }

        if (created > 0)
            await inventoryDb.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Demo clothing inventory seed finished. Items created={Created}, skipped={Skipped}, quantity={Quantity}.",
            created,
            skipped,
            defaultQuantity);

        return new SeedResult(created, skipped);
    }
}
