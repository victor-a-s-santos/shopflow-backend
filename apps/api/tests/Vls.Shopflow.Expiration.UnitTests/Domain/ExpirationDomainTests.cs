using FluentAssertions;
using Vls.Shopflow.CartCheckout.Domain.Entities;
using Vls.Shopflow.CartCheckout.Domain.Enums;
using Vls.Shopflow.Orders.Domain.Entities;
using Vls.Shopflow.Orders.Domain.Enums;
using Vls.Shopflow.PaymentsPix.Domain.Entities;
using Vls.Shopflow.PaymentsPix.Domain.Enums;

namespace Vls.Shopflow.Expiration.UnitTests.Domain;

public sealed class ExpirationDomainTests
{
    private static CheckoutSession PendingSession(DateTimeOffset expiresAt)
    {
        var item = CheckoutSessionItem.Create(
            Guid.NewGuid(),
            "Produto",
            "produto",
            Guid.NewGuid(),
            "SKU-1",
            1,
            10m,
            Guid.NewGuid());

        return CheckoutSession.CreatePending(
            "Cliente",
            "cliente@test.com",
            "11999990000",
            "01001000",
            "Rua",
            "1",
            null,
            "Centro",
            "São Paulo",
            "SP",
            expiresAt,
            new[] { item });
    }

    [Fact]
    public void CheckoutSession_Expire_SetsExpiredStatus()
    {
        var session = PendingSession(DateTimeOffset.UtcNow.AddMinutes(15));
        session.Expire();
        session.Status.Should().Be(CheckoutSessionStatus.Expired);
    }

    [Fact]
    public void CheckoutSession_Expire_IsIdempotent()
    {
        var session = PendingSession(DateTimeOffset.UtcNow.AddMinutes(15));
        session.Expire();
        session.Expire();
        session.Status.Should().Be(CheckoutSessionStatus.Expired);
    }

    [Fact]
    public void Order_Expire_FromPendingPayment_SetsExpired()
    {
        var order = Order.CreatePendingPayment(
            Guid.NewGuid(),
            "Cliente",
            "cliente@test.com",
            "11999990000",
            "01001000",
            "Rua",
            "1",
            null,
            "Centro",
            "São Paulo",
            "SP",
            100m,
            null,
            100m,
            new[] { OrderItem.Create(Guid.NewGuid(), "Item", "SKU", 1, 100m) });

        order.Expire();
        order.Status.Should().Be(OrderStatus.Expired);
    }

    [Fact]
    public void Order_Expire_IsIdempotent()
    {
        var order = Order.CreatePendingPayment(
            Guid.NewGuid(),
            "Cliente",
            "cliente@test.com",
            "11999990000",
            "01001000",
            "Rua",
            "1",
            null,
            "Centro",
            "São Paulo",
            "SP",
            100m,
            null,
            100m,
            new[] { OrderItem.Create(Guid.NewGuid(), "Item", "SKU", 1, 100m) });

        order.Expire();
        order.Expire();
        order.Status.Should().Be(OrderStatus.Expired);
    }

    [Fact]
    public void PixPayment_Expire_FromPending_SetsExpired()
    {
        var payment = PixPayment.CreatePending(
            Guid.NewGuid(),
            50m,
            PixPaymentProviderType.Fake,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            DateTimeOffset.UtcNow.AddMinutes(15));

        payment.Expire();
        payment.Status.Should().Be(PixPaymentStatus.Expired);
    }
}
