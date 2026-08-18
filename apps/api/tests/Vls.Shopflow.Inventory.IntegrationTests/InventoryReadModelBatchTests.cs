using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.Inventory.Domain.Entities;
using Vls.Shopflow.Inventory.Infrastructure;
using Vls.Shopflow.Inventory.Infrastructure.Repositories;

namespace Vls.Shopflow.Inventory.IntegrationTests;

public sealed class InventoryReadModelBatchTests
{
    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("SHOPFLOW_TEST_DB")
        ?? "Host=localhost;Port=5432;Database=shopflow;Username=postgres;Password=postgres";

    private static async Task<bool> CanConnectAsync()
    {
        try
        {
            await using var db = CreateContext();
            return await db.Database.CanConnectAsync();
        }
        catch
        {
            return false;
        }
    }

    private static InventoryDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseNpgsql(ConnectionString, npg =>
                npg.MigrationsHistoryTable("__EFMigrationsHistory", "inventory"))
            .Options;

        return new InventoryDbContext(options);
    }

    [Fact]
    public async Task GetBySkuIdsAsync_ReturnsExistingItemsWithoutCreatingMissing()
    {
        if (!await CanConnectAsync())
            return;

        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();

        var skuA = Guid.NewGuid();
        var skuMissing = Guid.NewGuid();
        var item = InventoryItem.Create(skuA, initialQuantity: 25, isInitialStock: true);
        // Simulate reserved stock via domain reserve if available, or set through Create then reserve.
        setup.InventoryItems.Add(item);
        await setup.SaveChangesAsync();

        await using var opsDb = CreateContext();
        var ops = new InventoryAtomicOperations(opsDb);
        await ops.ReserveAsync(skuA, 5, DateTimeOffset.UtcNow.AddMinutes(30), CancellationToken.None);

        await using var readDb = CreateContext();
        var readModel = new InventoryReadModel(readDb);

        var found = await readModel.GetBySkuIdsAsync([skuA, skuMissing], CancellationToken.None);

        found.Should().ContainSingle(x => x.SkuId == skuA);
        var dto = found.Single(x => x.SkuId == skuA);
        dto.QuantityOnHand.Should().Be(25);
        dto.QuantityReserved.Should().Be(5);
        dto.AvailableQuantity.Should().Be(20);
        found.Should().NotContain(x => x.SkuId == skuMissing);

        // Read path must not invent rows for missing SKUs.
        (await readDb.InventoryItems.AnyAsync(i => i.SkuId == skuMissing)).Should().BeFalse();
        (await readDb.InventoryItems.CountAsync(i => i.SkuId == skuA)).Should().Be(1);
    }
}
