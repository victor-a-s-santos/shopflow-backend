using Vls.Shopflow.BuildingBlocks.Domain.Entities;
using Vls.Shopflow.Inventory.Domain.Enums;
using Vls.Shopflow.Inventory.Domain.Exceptions;

namespace Vls.Shopflow.Inventory.Domain.Entities;

public sealed class InventoryItem : Entity<Guid>
{
    private readonly List<StockMovement> _movements = new();
    private readonly List<StockReservation> _reservations = new();

    public Guid SkuId { get; private set; }
    public int QuantityOnHand { get; private set; }
    public int QuantityReserved { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public int AvailableQuantity => QuantityOnHand - QuantityReserved;

    public IReadOnlyCollection<StockMovement> Movements => _movements.AsReadOnly();
    public IReadOnlyCollection<StockReservation> Reservations => _reservations.AsReadOnly();

    private InventoryItem() { }

    /// <summary>
    /// Creates inventory with optional initial quantity. Used by explicit create and add-stock auto-create.
    /// </summary>
    public static InventoryItem Create(
        Guid skuId,
        int initialQuantity = 0,
        string? reason = null,
        bool isInitialStock = false)
    {
        if (initialQuantity < 0)
            throw new InvalidStockQuantityException("Initial quantity cannot be negative.");

        var now = DateTimeOffset.UtcNow;
        var item = new InventoryItem
        {
            Id = Guid.NewGuid(),
            SkuId = skuId,
            QuantityOnHand = initialQuantity,
            QuantityReserved = 0,
            CreatedAt = now,
            UpdatedAt = now
        };

        if (initialQuantity > 0)
        {
            var movementType = isInitialStock
                ? StockMovementType.InitialStockAdded
                : StockMovementType.StockAdded;

            item._movements.Add(StockMovement.Create(
                item.Id,
                skuId,
                movementType,
                initialQuantity,
                reason ?? (isInitialStock ? "Initial stock" : null)));
        }

        return item;
    }

    public StockMovement AddStock(int quantity, string? reason)
    {
        EnsurePositive(quantity);
        QuantityOnHand += quantity;
        Touch();

        var movement = StockMovement.Create(Id, SkuId, StockMovementType.StockAdded, quantity, reason);
        _movements.Add(movement);
        return movement;
    }

    public StockMovement RemoveStock(int quantity, string? reason)
    {
        EnsurePositive(quantity);

        if (quantity > AvailableQuantity)
            throw new InsufficientStockException(SkuId, quantity, AvailableQuantity);

        QuantityOnHand -= quantity;
        Touch();

        var movement = StockMovement.Create(Id, SkuId, StockMovementType.StockRemoved, quantity, reason);
        _movements.Add(movement);
        return movement;
    }

    /// <summary>
    /// Used only after atomic DB reserve; quantities already updated in database.
    /// </summary>
    internal StockReservation AttachPendingReservation(int quantity, DateTimeOffset? expiresAt)
    {
        var reservation = StockReservation.CreatePending(Id, SkuId, quantity, expiresAt);
        _reservations.Add(reservation);
        return reservation;
    }

    internal StockMovement RecordMovement(StockMovementType type, int quantity, string? reason)
    {
        var movement = StockMovement.Create(Id, SkuId, type, quantity, reason);
        _movements.Add(movement);
        return movement;
    }

    internal void ApplyAtomicReservation(int quantity) => QuantityReserved += quantity;

    internal void ApplyAtomicRemoval(int quantity) => QuantityOnHand -= quantity;

    internal void ApplyAtomicConfirm(int quantity)
    {
        QuantityReserved -= quantity;
        QuantityOnHand -= quantity;
    }

    internal void ApplyAtomicCancel(int quantity) => QuantityReserved -= quantity;

    internal void ReloadQuantities(int onHand, int reserved, DateTimeOffset updatedAt)
    {
        QuantityOnHand = onHand;
        QuantityReserved = reserved;
        UpdatedAt = updatedAt;
    }

    public StockMovement ConfirmReservation(StockReservation reservation)
    {
        EnsureReservationBelongsToItem(reservation);
        reservation.Confirm();

        if (reservation.Quantity > QuantityReserved)
            throw new InsufficientStockException(SkuId, reservation.Quantity, QuantityReserved);

        QuantityReserved -= reservation.Quantity;
        QuantityOnHand -= reservation.Quantity;
        Touch();

        return RecordMovement(StockMovementType.ReservationConfirmed, reservation.Quantity, null);
    }

    public StockMovement CancelReservation(StockReservation reservation)
    {
        EnsureReservationBelongsToItem(reservation);
        reservation.Cancel();

        if (reservation.Quantity > QuantityReserved)
            throw new InsufficientStockException(SkuId, reservation.Quantity, QuantityReserved);

        QuantityReserved -= reservation.Quantity;
        Touch();

        return RecordMovement(StockMovementType.ReservationCanceled, reservation.Quantity, null);
    }

    private void EnsureReservationBelongsToItem(StockReservation reservation)
    {
        if (reservation.InventoryItemId != Id)
            throw new InvalidStockReservationStatusException(
                reservation.Id,
                "Reservation does not belong to this inventory item.");
    }

    private static void EnsurePositive(int quantity)
    {
        if (quantity <= 0)
            throw new InvalidStockQuantityException("Quantity must be greater than zero.");
    }

    internal void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
