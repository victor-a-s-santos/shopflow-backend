using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Vls.Shopflow.Inventory.Application.Repositories;
using Vls.Shopflow.Inventory.Domain.Entities;
using Vls.Shopflow.Inventory.Domain.Enums;
using Vls.Shopflow.Inventory.Domain.Exceptions;

namespace Vls.Shopflow.Inventory.Infrastructure.Repositories;

/// <summary>
/// Concurrency strategy: conditional SQL UPDATE on PostgreSQL within explicit transactions.
/// A row is updated only when invariants hold (available quantity, pending status).
/// This avoids lost updates without requiring optimistic tokens on the aggregate for hot paths.
/// </summary>
public sealed class InventoryAtomicOperations(InventoryDbContext db) : IInventoryAtomicOperations
{
    public async Task<Guid> ReserveAsync(
        Guid skuId,
        int quantity,
        DateTimeOffset? expiresAt,
        CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var rows = await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE inventory.inventory_items
                SET "QuantityReserved" = "QuantityReserved" + {quantity},
                    "UpdatedAt" = {now}
                WHERE "SkuId" = {skuId}
                  AND ("QuantityOnHand" - "QuantityReserved") >= {quantity}
                """, ct);

            if (rows == 0)
                await ThrowReserveFailureAsync(skuId, quantity, ct);

            var item = await db.InventoryItems.FirstAsync(i => i.SkuId == skuId, ct);
            await db.Entry(item).ReloadAsync(ct);

            var reservation = StockReservation.CreatePending(item.Id, skuId, quantity, expiresAt);
            var movement = StockMovement.Create(
                item.Id, skuId, StockMovementType.StockReserved, quantity, null);

            db.StockReservations.Add(reservation);
            db.StockMovements.Add(movement);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return reservation.Id;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task RemoveStockAsync(Guid skuId, int quantity, string? reason, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var rows = await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE inventory.inventory_items
                SET "QuantityOnHand" = "QuantityOnHand" - {quantity},
                    "UpdatedAt" = {now}
                WHERE "SkuId" = {skuId}
                  AND ("QuantityOnHand" - "QuantityReserved") >= {quantity}
                """, ct);

            if (rows == 0)
                await ThrowRemoveFailureAsync(skuId, quantity, ct);

            var item = await db.InventoryItems.FirstAsync(i => i.SkuId == skuId, ct);
            var movement = StockMovement.Create(
                item.Id, skuId, StockMovementType.StockRemoved, quantity, reason);

            db.StockMovements.Add(movement);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task ConfirmReservationAsync(Guid reservationId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var reservation = await db.StockReservations
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == reservationId, ct);

            if (reservation is null)
                throw new StockReservationNotFoundException(reservationId);

            var now = DateTimeOffset.UtcNow;
            var statusUpdated = await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE inventory.stock_reservations
                SET "Status" = {nameof(StockReservationStatus.Confirmed)},
                    "ConfirmedAt" = {now}
                WHERE "Id" = {reservationId}
                  AND "Status" = {nameof(StockReservationStatus.Pending)}
                """, ct);

            if (statusUpdated == 0)
                throw new InvalidStockReservationStatusException(
                    reservationId,
                    "Only pending reservations can be confirmed.");

            var inventoryUpdated = await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE inventory.inventory_items
                SET "QuantityReserved" = "QuantityReserved" - {reservation.Quantity},
                    "QuantityOnHand" = "QuantityOnHand" - {reservation.Quantity},
                    "UpdatedAt" = {now}
                WHERE "Id" = {reservation.InventoryItemId}
                  AND "QuantityReserved" >= {reservation.Quantity}
                  AND "QuantityOnHand" >= {reservation.Quantity}
                """, ct);

            if (inventoryUpdated == 0)
                throw new InsufficientStockException(
                    reservation.SkuId, reservation.Quantity, 0);

            var movement = StockMovement.Create(
                reservation.InventoryItemId,
                reservation.SkuId,
                StockMovementType.ReservationConfirmed,
                reservation.Quantity,
                null);

            db.StockMovements.Add(movement);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task CancelReservationAsync(Guid reservationId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var reservation = await db.StockReservations
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == reservationId, ct);

            if (reservation is null)
                throw new StockReservationNotFoundException(reservationId);

            var now = DateTimeOffset.UtcNow;
            var statusUpdated = await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE inventory.stock_reservations
                SET "Status" = {nameof(StockReservationStatus.Canceled)},
                    "CanceledAt" = {now}
                WHERE "Id" = {reservationId}
                  AND "Status" = {nameof(StockReservationStatus.Pending)}
                """, ct);

            if (statusUpdated == 0)
                throw new InvalidStockReservationStatusException(
                    reservationId,
                    "Only pending reservations can be canceled.");

            var inventoryUpdated = await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE inventory.inventory_items
                SET "QuantityReserved" = "QuantityReserved" - {reservation.Quantity},
                    "UpdatedAt" = {now}
                WHERE "Id" = {reservation.InventoryItemId}
                  AND "QuantityReserved" >= {reservation.Quantity}
                """, ct);

            if (inventoryUpdated == 0)
                throw new InsufficientStockException(
                    reservation.SkuId, reservation.Quantity, 0);

            var movement = StockMovement.Create(
                reservation.InventoryItemId,
                reservation.SkuId,
                StockMovementType.ReservationCanceled,
                reservation.Quantity,
                null);

            db.StockMovements.Add(movement);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    private async Task ThrowReserveFailureAsync(Guid skuId, int quantity, CancellationToken ct)
    {
        var item = await db.InventoryItems.AsNoTracking()
            .FirstOrDefaultAsync(i => i.SkuId == skuId, ct);

        if (item is null)
            throw new InventoryItemNotFoundException(skuId);

        throw new InsufficientStockException(
            skuId, quantity, item.QuantityOnHand - item.QuantityReserved);
    }

    private async Task ThrowRemoveFailureAsync(Guid skuId, int quantity, CancellationToken ct)
    {
        var item = await db.InventoryItems.AsNoTracking()
            .FirstOrDefaultAsync(i => i.SkuId == skuId, ct);

        if (item is null)
            throw new InventoryItemNotFoundException(skuId);

        throw new InsufficientStockException(
            skuId, quantity, item.QuantityOnHand - item.QuantityReserved);
    }
}
