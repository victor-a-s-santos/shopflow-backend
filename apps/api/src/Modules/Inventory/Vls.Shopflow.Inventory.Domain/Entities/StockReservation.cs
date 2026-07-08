using Vls.Shopflow.BuildingBlocks.Domain.Entities;
using Vls.Shopflow.Inventory.Domain.Enums;
using Vls.Shopflow.Inventory.Domain.Exceptions;

namespace Vls.Shopflow.Inventory.Domain.Entities;

public sealed class StockReservation : Entity<Guid>
{
    public Guid InventoryItemId { get; private set; }
    public InventoryItem InventoryItem { get; private set; } = default!;

    public Guid SkuId { get; private set; }
    public int Quantity { get; private set; }
    public StockReservationStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }
    public DateTimeOffset? CanceledAt { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }

    private StockReservation() { }

    public static StockReservation CreatePending(
        Guid inventoryItemId,
        Guid skuId,
        int quantity,
        DateTimeOffset? expiresAt)
    {
        if (quantity <= 0)
            throw new InvalidStockQuantityException("Reservation quantity must be greater than zero.");

        return new StockReservation
        {
            Id = Guid.NewGuid(),
            InventoryItemId = inventoryItemId,
            SkuId = skuId,
            Quantity = quantity,
            Status = StockReservationStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = expiresAt
        };
    }

    internal void Confirm()
    {
        if (Status != StockReservationStatus.Pending)
            throw new InvalidStockReservationStatusException(
                Id,
                "Only pending reservations can be confirmed.");

        Status = StockReservationStatus.Confirmed;
        ConfirmedAt = DateTimeOffset.UtcNow;
    }

    internal void Cancel()
    {
        if (Status != StockReservationStatus.Pending)
            throw new InvalidStockReservationStatusException(
                Id,
                "Only pending reservations can be canceled.");

        Status = StockReservationStatus.Canceled;
        CanceledAt = DateTimeOffset.UtcNow;
    }
}
