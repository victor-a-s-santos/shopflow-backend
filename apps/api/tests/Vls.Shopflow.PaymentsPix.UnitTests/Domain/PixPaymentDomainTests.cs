using FluentAssertions;
using Vls.Shopflow.PaymentsPix.Domain.Entities;
using Vls.Shopflow.PaymentsPix.Domain.Enums;

namespace Vls.Shopflow.PaymentsPix.UnitTests.Domain;

public sealed class PixPaymentDomainTests
{
    [Fact]
    public void CreatePending_WithValidData_CreatesPendingPayment()
    {
        var orderId = Guid.NewGuid();

        var payment = PixPayment.CreatePending(
            orderId,
            150m,
            PixPaymentProviderType.Fake,
            "fake-dev-id",
            null,
            null,
            null,
            DateTimeOffset.UtcNow.AddMinutes(30));

        payment.Id.Should().NotBeEmpty();
        payment.OrderId.Should().Be(orderId);
        payment.Amount.Should().Be(150m);
        payment.Status.Should().Be(PixPaymentStatus.Pending);
        payment.Provider.Should().Be(PixPaymentProviderType.Fake);
        payment.PaidAt.Should().BeNull();
    }

    [Fact]
    public void CreatePending_WithZeroAmount_Throws()
    {
        var act = () => PixPayment.CreatePending(
            Guid.NewGuid(),
            0m,
            PixPaymentProviderType.Fake,
            null,
            null,
            null,
            null,
            null);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CreatePending_WithNegativeAmount_Throws()
    {
        var act = () => PixPayment.CreatePending(
            Guid.NewGuid(),
            -10m,
            PixPaymentProviderType.Fake,
            null,
            null,
            null,
            null,
            null);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CreatePending_DoesNotMarkAsPaid()
    {
        var payment = PixPayment.CreatePending(
            Guid.NewGuid(),
            99m,
            PixPaymentProviderType.NotConfigured,
            null,
            null,
            null,
            null,
            null);

        payment.Status.Should().Be(PixPaymentStatus.Pending);
        payment.PaidAt.Should().BeNull();
    }
}
