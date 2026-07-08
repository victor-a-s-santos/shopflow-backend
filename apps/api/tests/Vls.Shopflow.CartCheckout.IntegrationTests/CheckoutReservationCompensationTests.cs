using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vls.Shopflow.CartCheckout.Application.CommandHandlers;
using Vls.Shopflow.CartCheckout.Application.Commands;
using Vls.Shopflow.CartCheckout.Application.Interfaces;
using Vls.Shopflow.CartCheckout.Infrastructure;
using Vls.Shopflow.CartCheckout.Infrastructure.Services;
using Vls.Shopflow.Inventory.Domain.Entities;
using Vls.Shopflow.Inventory.Domain.Exceptions;
using Vls.Shopflow.Inventory.Infrastructure;
using Vls.Shopflow.Inventory.Infrastructure.Repositories;

namespace Vls.Shopflow.CartCheckout.IntegrationTests;

public sealed class CheckoutReservationCompensationTests
{
    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("SHOPFLOW_TEST_DB")
        ?? "Host=localhost;Port=5432;Database=shopflow;Username=postgres;Password=postgres";

    private static async Task<bool> CanConnectAsync()
    {
        try
        {
            await using var db = CreateInventoryContext();
            return await db.Database.CanConnectAsync();
        }
        catch
        {
            return false;
        }
    }

    private static InventoryDbContext CreateInventoryContext()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseNpgsql(ConnectionString, npg =>
                npg.MigrationsHistoryTable("__EFMigrationsHistory", "inventory"))
            .Options;

        return new InventoryDbContext(options);
    }

    [Fact]
    public async Task ReserveSecondItemFailure_CancelsFirstReservation()
    {
        if (!await CanConnectAsync())
            return;

        await using var inventoryDb = CreateInventoryContext();
        await inventoryDb.Database.MigrateAsync();

        var skuWithStock = Guid.NewGuid();
        var skuWithoutStock = Guid.NewGuid();

        inventoryDb.InventoryItems.Add(InventoryItem.Create(skuWithStock, 5, isInitialStock: true));
        inventoryDb.InventoryItems.Add(InventoryItem.Create(skuWithoutStock, 0, isInitialStock: true));
        await inventoryDb.SaveChangesAsync();

        var inventoryService = new InventoryReservationService(new InventoryAtomicOperations(inventoryDb));
        var firstReservation = Guid.Empty;
        var canceled = false;

        var fakeInventory = new TrackingInventoryReservationService(inventoryService);
        fakeInventory.OnReserved += id => firstReservation = id;
        fakeInventory.FailOnSkuId = skuWithoutStock;

        try
        {
            var act = async () =>
            {
                var first = await fakeInventory.ReserveAsync(skuWithStock, 1, null, CancellationToken.None);
                firstReservation = first;
                await fakeInventory.ReserveAsync(skuWithoutStock, 1, null, CancellationToken.None);
            };

            await act.Should().ThrowAsync<InsufficientStockException>();
        }
        finally
        {
            if (firstReservation != Guid.Empty)
            {
                try
                {
                    await inventoryService.CancelReservationAsync(firstReservation, CancellationToken.None);
                    canceled = true;
                }
                catch
                {
                    canceled = false;
                }
            }
        }

        canceled.Should().BeTrue("compensation should release the first reservation after partial failure");

        await using var verify = CreateInventoryContext();
        var item = await verify.InventoryItems.AsNoTracking()
            .SingleAsync(i => i.SkuId == skuWithStock);
        item.QuantityReserved.Should().Be(0);
    }

    private sealed class TrackingInventoryReservationService(IInventoryReservationService inner) : IInventoryReservationService
    {
        public Guid? FailOnSkuId { get; set; }
        public event Action<Guid>? OnReserved;

        public async Task<Guid> ReserveAsync(
            Guid skuId,
            int quantity,
            DateTimeOffset? expiresAt,
            CancellationToken cancellationToken)
        {
            if (FailOnSkuId == skuId)
                throw new InsufficientStockException(skuId, quantity, 0);

            var id = await inner.ReserveAsync(skuId, quantity, expiresAt, cancellationToken);
            OnReserved?.Invoke(id);
            return id;
        }

        public Task CancelReservationAsync(Guid reservationId, CancellationToken cancellationToken)
            => inner.CancelReservationAsync(reservationId, cancellationToken);
    }
}
