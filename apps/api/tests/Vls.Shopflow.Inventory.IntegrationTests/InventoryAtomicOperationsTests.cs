using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.Inventory.Domain.Entities;
using Vls.Shopflow.Inventory.Domain.Enums;
using Vls.Shopflow.Inventory.Domain.Exceptions;
using Vls.Shopflow.Inventory.Infrastructure;
using Vls.Shopflow.Inventory.Infrastructure.Repositories;

namespace Vls.Shopflow.Inventory.IntegrationTests;

public sealed class InventoryAtomicOperationsTests
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
    public async Task ReserveAsync_ConcurrentRequests_OnlyOneSucceedsWhenInsufficientStock()
    {
        if (!await CanConnectAsync())
        {
            // Skip when PostgreSQL is not available (CI without docker).
            return;
        }

        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();

        var skuId = Guid.NewGuid();
        var item = InventoryItem.Create(skuId, initialQuantity: 10, isInitialStock: true);
        setup.InventoryItems.Add(item);
        await setup.SaveChangesAsync();

        await using var db1 = CreateContext();
        await using var db2 = CreateContext();
        var ops1 = new InventoryAtomicOperations(db1);
        var ops2 = new InventoryAtomicOperations(db2);

        var task1 = ops1.ReserveAsync(skuId, 6, null, CancellationToken.None);
        var task2 = ops2.ReserveAsync(skuId, 6, null, CancellationToken.None);

        var results = await Task.WhenAll(
            SafeReserve(task1),
            SafeReserve(task2));

        results.Count(r => r.Success).Should().Be(1);
        results.Count(r => !r.Success).Should().Be(1);
        results.Single(r => !r.Success).Exception.Should().BeOfType<InsufficientStockException>();

        await using var verify = CreateContext();
        var updated = await verify.InventoryItems.AsNoTracking()
            .SingleAsync(i => i.SkuId == skuId);

        updated.QuantityReserved.Should().Be(6);
        updated.QuantityOnHand.Should().Be(10);
        (updated.QuantityReserved <= updated.QuantityOnHand).Should().BeTrue();

        var reservations = await verify.StockReservations.AsNoTracking()
            .Where(r => r.SkuId == skuId)
            .ToListAsync();
        reservations.Should().HaveCount(1);
        reservations[0].Status.Should().Be(StockReservationStatus.Pending);

        var movements = await verify.StockMovements.AsNoTracking()
            .Where(m => m.SkuId == skuId && m.Type == StockMovementType.StockReserved)
            .ToListAsync();
        movements.Should().HaveCount(1);
    }

    [Fact]
    public async Task ConfirmReservationAsync_WhenCalledTwice_IsIdempotent()
    {
        if (!await CanConnectAsync())
            return;

        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();

        var skuId = Guid.NewGuid();
        var item = InventoryItem.Create(skuId, initialQuantity: 10, isInitialStock: true);
        setup.InventoryItems.Add(item);
        await setup.SaveChangesAsync();

        await using var reserveDb = CreateContext();
        var reservationId = await new InventoryAtomicOperations(reserveDb)
            .ReserveAsync(skuId, 4, null, CancellationToken.None);

        await using var db1 = CreateContext();
        await using var db2 = CreateContext();
        var ops1 = new InventoryAtomicOperations(db1);
        var ops2 = new InventoryAtomicOperations(db2);

        await ops1.ConfirmReservationAsync(reservationId, CancellationToken.None);
        await ops2.ConfirmReservationAsync(reservationId, CancellationToken.None);

        await using var verify = CreateContext();
        var updated = await verify.InventoryItems.AsNoTracking()
            .SingleAsync(i => i.SkuId == skuId);
        updated.QuantityOnHand.Should().Be(6);
        updated.QuantityReserved.Should().Be(0);
    }

    [Fact]
    public async Task RemoveStockAsync_WhenInsufficient_Throws()
    {
        if (!await CanConnectAsync())
            return;

        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();

        var skuId = Guid.NewGuid();
        var item = InventoryItem.Create(skuId, initialQuantity: 5, isInitialStock: true);
        setup.InventoryItems.Add(item);
        await setup.SaveChangesAsync();

        await using var reserveDb = CreateContext();
        await new InventoryAtomicOperations(reserveDb)
            .ReserveAsync(skuId, 3, null, CancellationToken.None);

        await using var removeDb = CreateContext();
        var act = () => new InventoryAtomicOperations(removeDb)
            .RemoveStockAsync(skuId, 3, "Baixa", CancellationToken.None);

        await act.Should().ThrowAsync<InsufficientStockException>();
    }

    private static async Task<(bool Success, Exception? Exception)> SafeReserve(Task<Guid> task)
    {
        try
        {
            await task;
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex);
        }
    }
}
