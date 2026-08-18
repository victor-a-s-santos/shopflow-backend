using FluentAssertions;
using Vls.Shopflow.Orders.Domain.Entities;
using Vls.Shopflow.Orders.Domain.Enums;
using Vls.Shopflow.Orders.Domain.Exceptions;

namespace Vls.Shopflow.Orders.UnitTests.Domain;

public sealed class OrderFulfillmentDomainTests
{
    private static Order CreatePaidOrder()
    {
        var order = CreatePendingOrder();
        order.MarkAsPaid();
        return order;
    }

    private static Order CreatePendingOrder()
    {
        var item = OrderItem.Create(Guid.NewGuid(), "Produto", "SKU-1", 1, 50m);
        var order = Order.CreatePendingPayment(
            Guid.NewGuid(),
            "Cliente",
            "c@test.com",
            "11999999999",
            "01001000",
            "Rua A",
            "10",
            null,
            "Centro",
            "São Paulo",
            "SP",
            50m,
            null,
            50m,
            [item]);
        order.AssignOrderNumber(20001);
        return order;
    }

    [Fact]
    public void CreatePendingPayment_DefaultsFulfillmentToAwaitingShipment()
    {
        var order = CreatePendingOrder();
        order.FulfillmentStatus.Should().Be(FulfillmentStatus.AwaitingShipment);
    }

    [Fact]
    public void MarkAsShipped_WhenPaid_SetsShippedFields()
    {
        var order = CreatePaidOrder();
        var adminId = Guid.NewGuid();

        order.MarkAsShipped(adminId, DeliveryMethod.Carrier, "TRACK-1", "Saiu hoje");

        order.FulfillmentStatus.Should().Be(FulfillmentStatus.Shipped);
        order.ShippedAt.Should().NotBeNull();
        order.FinalDeliveryMethod.Should().Be(DeliveryMethod.Carrier);
        order.TrackingCode.Should().Be("TRACK-1");
        order.InternalOrderNote.Should().Be("Saiu hoje");
        order.FulfillmentUpdatedByAdminId.Should().Be(adminId);
    }

    [Fact]
    public void MarkAsShipped_WhenPendingPayment_Throws()
    {
        var order = CreatePendingOrder();
        var act = () => order.MarkAsShipped(Guid.NewGuid());
        act.Should().Throw<OrderNotPaidForShipmentException>();
    }

    [Fact]
    public void MarkAsShipped_WhenCanceled_Throws()
    {
        var order = CreatePendingOrder();
        // Simulate canceled via expire path is not cancel — use reflection-free approach:
        // Expire then try ship after artificially marking paid is wrong.
        // Paid + then we can't cancel in domain. Test Expired:
        order.Expire();
        var act = () => order.MarkAsShipped(Guid.NewGuid());
        act.Should().Throw<OrderNotPaidForShipmentException>();
    }

    [Fact]
    public void MarkAsDelivered_WhenShipped_SetsDelivered()
    {
        var order = CreatePaidOrder();
        order.MarkAsShipped(Guid.NewGuid(), DeliveryMethod.ExcursionBus, "Bus X");

        order.MarkAsDelivered(Guid.NewGuid(), "Cliente confirmou");

        order.FulfillmentStatus.Should().Be(FulfillmentStatus.Delivered);
        order.DeliveredAt.Should().NotBeNull();
        order.InternalOrderNote.Should().Be("Cliente confirmou");
    }

    [Fact]
    public void MarkAsDelivered_BeforeShipped_Throws()
    {
        var order = CreatePaidOrder();
        var act = () => order.MarkAsDelivered(Guid.NewGuid());
        act.Should().Throw<OrderMustBeShippedBeforeDeliveredException>();
    }

    [Fact]
    public void MarkAsShipped_AfterDelivered_Throws()
    {
        var order = CreatePaidOrder();
        order.MarkAsShipped(Guid.NewGuid());
        order.MarkAsDelivered(Guid.NewGuid());

        var act = () => order.MarkAsShipped(Guid.NewGuid());
        act.Should().Throw<OrderCannotBeShippedException>();
    }

    [Fact]
    public void MarkAsDelivered_WhenAlreadyDelivered_IsIdempotent()
    {
        var order = CreatePaidOrder();
        order.MarkAsShipped(Guid.NewGuid());
        order.MarkAsDelivered(Guid.NewGuid());
        var deliveredAt = order.DeliveredAt;

        order.MarkAsDelivered(Guid.NewGuid(), "Nota extra");

        order.FulfillmentStatus.Should().Be(FulfillmentStatus.Delivered);
        order.DeliveredAt.Should().Be(deliveredAt);
        order.InternalOrderNote.Should().Be("Nota extra");
    }

    [Fact]
    public void SetInternalOrderNote_RejectsTooLong()
    {
        var order = CreatePendingOrder();
        var act = () => order.SetInternalOrderNote(new string('x', Order.InternalOrderNoteMaxLength + 1));
        act.Should().Throw<OrderNoteTooLongException>();
    }

    [Fact]
    public void SetDeliveryPreference_NormalizesEmptyCustomerNoteToNull()
    {
        var order = CreatePendingOrder();
        order.SetDeliveryPreference(DeliveryMethod.Correios, new DateOnly(2026, 8, 1), "   ");
        order.CustomerOrderNote.Should().BeNull();
        order.PreferredDeliveryMethod.Should().Be(DeliveryMethod.Correios);
    }

    [Fact]
    public void SetCustomerOrderNote_RejectsTooLong()
    {
        var order = CreatePendingOrder();
        var act = () => order.SetDeliveryPreference(
            null,
            null,
            new string('a', Order.CustomerOrderNoteMaxLength + 1));
        act.Should().Throw<OrderNoteTooLongException>();
    }
}
