using FluentAssertions;
using Vls.Shopflow.PaymentsPix.Domain.Entities;
using Vls.Shopflow.PaymentsPix.Domain.Enums;

namespace Vls.Shopflow.PaymentsPix.UnitTests.Domain;

public sealed class PixPaymentDomainTests
{
    private static PixPayment CreatePending(
        Guid? orderId = null,
        decimal amount = 150m,
        PixPaymentProviderType provider = PixPaymentProviderType.Fake,
        string? providerOrderId = "fake-ord",
        DateTimeOffset? expiresAt = null)
        => PixPayment.CreatePending(
            orderId ?? Guid.NewGuid(),
            amount,
            provider,
            providerOrderId,
            providerOrderId is null ? null : "fake-pay",
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
            expiresAt ?? DateTimeOffset.UtcNow.AddMinutes(30));

    [Fact]
    public void CreatePending_WithValidData_CreatesPendingPayment()
    {
        var orderId = Guid.NewGuid();

        var payment = CreatePending(orderId);

        payment.Id.Should().NotBeEmpty();
        payment.OrderId.Should().Be(orderId);
        payment.Amount.Should().Be(150m);
        payment.Status.Should().Be(PixPaymentStatus.Pending);
        payment.Provider.Should().Be(PixPaymentProviderType.Fake);
        payment.ProviderOrderId.Should().Be("fake-ord");
        payment.ProviderTransactionId.Should().Be("fake-pay");
        payment.PaidAt.Should().BeNull();
    }

    [Fact]
    public void MarkAsPaid_FromPending_SetsPaidStatus()
    {
        var payment = CreatePending(provider: PixPaymentProviderType.MercadoPago, providerOrderId: "ORD1");

        var approvedAt = DateTimeOffset.UtcNow;
        payment.MarkAsPaid("processed", "accredited", "processed", "accredited", approvedAt, "ORD1", "PAY1");

        payment.Status.Should().Be(PixPaymentStatus.Paid);
        payment.PaidAt.Should().Be(approvedAt);
        payment.ProviderStatus.Should().Be("processed");
        payment.ProviderStatusDetail.Should().Be("accredited");
        payment.ProviderTransactionId.Should().Be("PAY1");
    }

    [Fact]
    public void MarkAsPaid_WhenAlreadyPaid_IsIdempotent()
    {
        var payment = CreatePending(provider: PixPaymentProviderType.MercadoPago, providerOrderId: "ORD1");

        payment.MarkAsPaid("processed", "accredited", "processed", "accredited", DateTimeOffset.UtcNow);
        var paidAt = payment.PaidAt;

        payment.MarkAsPaid("processed", "accredited", "processed", "accredited", DateTimeOffset.UtcNow.AddMinutes(1));

        payment.Status.Should().Be(PixPaymentStatus.Paid);
        payment.PaidAt.Should().Be(paidAt);
    }

    [Fact]
    public void CreatePending_WithZeroAmount_Throws()
    {
        var act = () => CreatePending(amount: 0m);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CreatePending_WithNegativeAmount_Throws()
    {
        var act = () => CreatePending(amount: -10m);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CreatePending_DoesNotMarkAsPaid()
    {
        var payment = CreatePending(provider: PixPaymentProviderType.NotConfigured, providerOrderId: null);

        payment.Status.Should().Be(PixPaymentStatus.Pending);
        payment.PaidAt.Should().BeNull();
    }
}
