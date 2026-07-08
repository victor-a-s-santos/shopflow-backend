using FluentAssertions;
using Vls.Shopflow.Inventory.Domain.Entities;
using Vls.Shopflow.Inventory.Domain.Enums;
using Vls.Shopflow.Inventory.Domain.Exceptions;

namespace Vls.Shopflow.Inventory.UnitTests.Domain;

public sealed class InventoryItemTests
{
    [Fact]
    public void Create_WithInitialQuantity_CreatesInitialStockMovement()
    {
        var skuId = Guid.NewGuid();

        var item = InventoryItem.Create(skuId, initialQuantity: 10, isInitialStock: true);

        item.SkuId.Should().Be(skuId);
        item.QuantityOnHand.Should().Be(10);
        item.QuantityReserved.Should().Be(0);
        item.AvailableQuantity.Should().Be(10);
        item.Movements.Should().ContainSingle(m =>
            m.Type == StockMovementType.InitialStockAdded && m.Quantity == 10);
    }

    [Fact]
    public void Create_WithZeroQuantity_DoesNotCreateMovement()
    {
        var item = InventoryItem.Create(Guid.NewGuid(), initialQuantity: 0, isInitialStock: true);

        item.QuantityOnHand.Should().Be(0);
        item.Movements.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithNegativeQuantity_Throws()
    {
        var act = () => InventoryItem.Create(Guid.NewGuid(), initialQuantity: -1);

        act.Should().Throw<InvalidStockQuantityException>();
    }

    [Fact]
    public void AddStock_IncrementsOnHandAndCreatesMovement()
    {
        var item = InventoryItem.Create(Guid.NewGuid(), initialQuantity: 5, isInitialStock: true);

        var movement = item.AddStock(3, "Reposição");

        item.QuantityOnHand.Should().Be(8);
        movement.Type.Should().Be(StockMovementType.StockAdded);
        movement.Quantity.Should().Be(3);
        item.Movements.Should().HaveCount(2);
    }

    [Fact]
    public void RemoveStock_WhenAvailable_ReducesOnHand()
    {
        var item = InventoryItem.Create(Guid.NewGuid(), initialQuantity: 10, isInitialStock: true);

        var movement = item.RemoveStock(4, "Baixa");

        item.QuantityOnHand.Should().Be(6);
        movement.Type.Should().Be(StockMovementType.StockRemoved);
    }

    [Fact]
    public void RemoveStock_WhenInsufficient_Throws()
    {
        var skuId = Guid.NewGuid();
        var item = InventoryItem.Create(skuId, initialQuantity: 5, isInitialStock: true);
        item.ApplyAtomicReservation(3);

        var act = () => item.RemoveStock(3, "Baixa");

        act.Should().Throw<InsufficientStockException>()
            .Which.SkuId.Should().Be(skuId);
    }

    [Fact]
    public void ConfirmReservation_ReducesOnHandAndReserved()
    {
        var item = InventoryItem.Create(Guid.NewGuid(), initialQuantity: 10, isInitialStock: true);
        var reservation = item.AttachPendingReservation(4, null);
        item.ApplyAtomicReservation(4);

        var movement = item.ConfirmReservation(reservation);

        item.QuantityOnHand.Should().Be(6);
        item.QuantityReserved.Should().Be(0);
        reservation.Status.Should().Be(StockReservationStatus.Confirmed);
        movement.Type.Should().Be(StockMovementType.ReservationConfirmed);
    }

    [Fact]
    public void CancelReservation_ReducesReservedOnly()
    {
        var item = InventoryItem.Create(Guid.NewGuid(), initialQuantity: 10, isInitialStock: true);
        var reservation = item.AttachPendingReservation(3, null);
        item.ApplyAtomicReservation(3);

        var movement = item.CancelReservation(reservation);

        item.QuantityOnHand.Should().Be(10);
        item.QuantityReserved.Should().Be(0);
        reservation.Status.Should().Be(StockReservationStatus.Canceled);
        movement.Type.Should().Be(StockMovementType.ReservationCanceled);
    }

    [Fact]
    public void ConfirmReservation_WhenAlreadyConfirmed_Throws()
    {
        var item = InventoryItem.Create(Guid.NewGuid(), initialQuantity: 10, isInitialStock: true);
        var reservation = item.AttachPendingReservation(2, null);
        item.ApplyAtomicReservation(2);
        item.ConfirmReservation(reservation);

        var act = () => item.ConfirmReservation(reservation);

        act.Should().Throw<InvalidStockReservationStatusException>();
    }

    [Fact]
    public void CancelReservation_WhenAlreadyConfirmed_Throws()
    {
        var item = InventoryItem.Create(Guid.NewGuid(), initialQuantity: 10, isInitialStock: true);
        var reservation = item.AttachPendingReservation(2, null);
        item.ApplyAtomicReservation(2);
        item.ConfirmReservation(reservation);

        var act = () => item.CancelReservation(reservation);

        act.Should().Throw<InvalidStockReservationStatusException>();
    }
}
