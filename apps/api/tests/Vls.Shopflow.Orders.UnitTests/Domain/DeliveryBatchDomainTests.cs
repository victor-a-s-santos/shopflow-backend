using FluentAssertions;
using Vls.Shopflow.Orders.Application.Services;
using Vls.Shopflow.Orders.Domain.Constants;
using Vls.Shopflow.Orders.Domain.Entities;
using Vls.Shopflow.Orders.Domain.Enums;
using Vls.Shopflow.Orders.Domain.Exceptions;

namespace Vls.Shopflow.Orders.UnitTests.Domain;

public sealed class DeliveryBatchDomainTests
{
    private static Order PaidOrder(
        Guid? customerUserId = null,
        string email = "loja@test.com",
        string phone = "11999999999",
        string street = "Rua A",
        long orderNumber = 10001)
    {
        var item = OrderItem.Create(Guid.NewGuid(), "Produto", "SKU-1", 1, 50m);
        var order = Order.CreatePendingPayment(
            Guid.NewGuid(),
            "Loja Silva",
            email,
            phone,
            "01001000",
            street,
            "10",
            null,
            "Centro",
            "São Paulo",
            "SP",
            50m,
            null,
            50m,
            [item],
            customerUserId);
        order.AssignOrderNumber(orderNumber);
        order.MarkAsPaid();
        return order;
    }

    [Fact]
    public void Create_WithTwoPaidOrders_SameCustomerUserId_Succeeds()
    {
        var userId = Guid.NewGuid();
        var a = PaidOrder(userId, orderNumber: 10001);
        var b = PaidOrder(userId, orderNumber: 10002);
        var identity = DeliveryBatchGroupingRules.ResolveIdentity([a, b]);

        var batch = DeliveryBatch.CreateAwaitingShipment(
            [a.Id, b.Id],
            identity.CustomerUserId,
            identity.Name,
            identity.Email,
            identity.Phone,
            hasDifferentDeliveryAddresses: false,
            createdByAdminId: Guid.NewGuid());

        batch.AssignBatchNumber(30001);
        batch.Status.Should().Be(DeliveryBatchStatus.AwaitingShipment);
        batch.Orders.Should().HaveCount(2);
        batch.BatchNumber.Should().Be(30001);
    }

    [Fact]
    public void Create_WithGuestSameEmailPhone_Succeeds()
    {
        var a = PaidOrder(null, "guest@test.com", "11988887777", orderNumber: 10001);
        var b = PaidOrder(null, "guest@test.com", "(11) 98888-7777", orderNumber: 10002);
        var identity = DeliveryBatchGroupingRules.ResolveIdentity([a, b]);

        var batch = DeliveryBatch.CreateAwaitingShipment(
            [a.Id, b.Id],
            identity.CustomerUserId,
            identity.Name,
            identity.Email,
            identity.Phone,
            false,
            Guid.NewGuid());

        batch.CustomerUserId.Should().BeNull();
        batch.CustomerEmailNormalized.Should().Be("guest@test.com");
        batch.CustomerPhoneNormalized.Should().Be("11988887777");
    }

    [Fact]
    public void ResolveIdentity_DifferentCustomerUserIds_Throws()
    {
        var a = PaidOrder(Guid.NewGuid(), orderNumber: 1);
        var b = PaidOrder(Guid.NewGuid(), orderNumber: 2);
        var act = () => DeliveryBatchGroupingRules.ResolveIdentity([a, b]);
        act.Should().Throw<DeliveryBatchException>()
            .Which.Code.Should().Be(DeliveryBatchErrorCodes.CustomerMismatch);
    }

    [Fact]
    public void ResolveIdentity_DifferentGuestEmails_Throws()
    {
        var a = PaidOrder(null, "a@test.com", "11999999999", orderNumber: 1);
        var b = PaidOrder(null, "b@test.com", "11999999999", orderNumber: 2);
        var act = () => DeliveryBatchGroupingRules.ResolveIdentity([a, b]);
        act.Should().Throw<DeliveryBatchException>()
            .Which.Code.Should().Be(DeliveryBatchErrorCodes.CustomerMismatch);
    }

    [Fact]
    public void EnsureEligible_PendingPayment_Throws()
    {
        var item = OrderItem.Create(Guid.NewGuid(), "P", "S", 1, 10m);
        var order = Order.CreatePendingPayment(
            Guid.NewGuid(), "N", "e@t.com", "11999999999",
            "01001000", "Rua", "1", null, "B", "C", "SP", 10m, null, 10m, [item]);
        order.AssignOrderNumber(1);

        var act = () => DeliveryBatchGroupingRules.EnsureEligibleForBatch(order, alreadyInBatch: false);
        act.Should().Throw<DeliveryBatchException>()
            .Which.Code.Should().Be(DeliveryBatchErrorCodes.OrderNotPaid);
    }

    [Fact]
    public void EnsureEligible_AlreadyInBatch_Throws()
    {
        var order = PaidOrder(Guid.NewGuid());
        var act = () => DeliveryBatchGroupingRules.EnsureEligibleForBatch(order, alreadyInBatch: true);
        act.Should().Throw<DeliveryBatchException>()
            .Which.Code.Should().Be(DeliveryBatchErrorCodes.OrderAlreadyInBatch);
    }

    [Fact]
    public void EnsureEligible_AlreadyShipped_Throws()
    {
        var order = PaidOrder(Guid.NewGuid());
        order.MarkAsShipped(Guid.NewGuid());
        var act = () => DeliveryBatchGroupingRules.EnsureEligibleForBatch(order, false);
        act.Should().Throw<DeliveryBatchException>()
            .Which.Code.Should().Be(DeliveryBatchErrorCodes.OrderAlreadyShipped);
    }

    [Fact]
    public void Create_RequiresMinTwoOrders()
    {
        var act = () => DeliveryBatch.CreateAwaitingShipment(
            [Guid.NewGuid()],
            Guid.NewGuid(),
            "Nome",
            "e@t.com",
            "11999999999",
            false,
            Guid.NewGuid());

        act.Should().Throw<DeliveryBatchException>()
            .Which.Code.Should().Be(DeliveryBatchErrorCodes.MinOrdersRequired);
    }

    [Fact]
    public void HasDifferentAddresses_DetectsMismatch()
    {
        var a = PaidOrder(Guid.NewGuid(), street: "Rua A", orderNumber: 1);
        var b = PaidOrder(a.CustomerUserId, street: "Rua B", orderNumber: 2);
        var infos = DeliveryBatchGroupingRules.BuildAddressInfos([a, b]);
        DeliveryBatchGroupingRules.HasDifferentAddresses(infos).Should().BeTrue();
    }

    [Fact]
    public void MarkAsShipped_ThenDeliver_UpdatesStatus()
    {
        var a = PaidOrder(Guid.NewGuid(), orderNumber: 1);
        var b = PaidOrder(a.CustomerUserId, orderNumber: 2);
        var identity = DeliveryBatchGroupingRules.ResolveIdentity([a, b]);
        var batch = DeliveryBatch.CreateAwaitingShipment(
            [a.Id, b.Id], identity.CustomerUserId, identity.Name, identity.Email, identity.Phone,
            false, Guid.NewGuid());
        batch.AssignBatchNumber(30010);

        batch.MarkAsShipped(Guid.NewGuid(), DeliveryMethod.Carrier, "TRACK-1", "Nota batch");
        batch.Status.Should().Be(DeliveryBatchStatus.Shipped);
        batch.TrackingCode.Should().Be("TRACK-1");
        batch.DeliveryMethod.Should().Be(DeliveryMethod.Carrier);

        batch.MarkAsDelivered(Guid.NewGuid(), "Entregue");
        batch.Status.Should().Be(DeliveryBatchStatus.Delivered);
        batch.DeliveredAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkAsDelivered_WhenAwaiting_Throws()
    {
        var a = PaidOrder(Guid.NewGuid(), orderNumber: 1);
        var b = PaidOrder(a.CustomerUserId, orderNumber: 2);
        var identity = DeliveryBatchGroupingRules.ResolveIdentity([a, b]);
        var batch = DeliveryBatch.CreateAwaitingShipment(
            [a.Id, b.Id], identity.CustomerUserId, identity.Name, identity.Email, identity.Phone,
            false, Guid.NewGuid());
        batch.AssignBatchNumber(30011);

        var act = () => batch.MarkAsDelivered(Guid.NewGuid());
        act.Should().Throw<DeliveryBatchException>()
            .Which.Code.Should().Be(DeliveryBatchErrorCodes.MustBeShippedBeforeDelivered);
    }

    [Fact]
    public void MarkAsShipped_AfterDelivered_Throws()
    {
        var a = PaidOrder(Guid.NewGuid(), orderNumber: 1);
        var b = PaidOrder(a.CustomerUserId, orderNumber: 2);
        var identity = DeliveryBatchGroupingRules.ResolveIdentity([a, b]);
        var batch = DeliveryBatch.CreateAwaitingShipment(
            [a.Id, b.Id], identity.CustomerUserId, identity.Name, identity.Email, identity.Phone,
            false, Guid.NewGuid());
        batch.AssignBatchNumber(30012);
        batch.MarkAsShipped(Guid.NewGuid());
        batch.MarkAsDelivered(Guid.NewGuid());

        var act = () => batch.MarkAsShipped(Guid.NewGuid());
        act.Should().Throw<DeliveryBatchException>()
            .Which.Code.Should().Be(DeliveryBatchErrorCodes.AlreadyDelivered);
    }
}
