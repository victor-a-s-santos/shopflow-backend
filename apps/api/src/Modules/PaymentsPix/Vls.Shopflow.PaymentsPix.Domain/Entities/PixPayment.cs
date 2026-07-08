using Vls.Shopflow.BuildingBlocks.Domain.Entities;
using Vls.Shopflow.PaymentsPix.Domain.Enums;

namespace Vls.Shopflow.PaymentsPix.Domain.Entities;

public sealed class PixPayment : Entity<Guid>
{
    public Guid OrderId { get; private set; }
    public decimal Amount { get; private set; }
    public PixPaymentStatus Status { get; private set; }
    public PixPaymentProviderType Provider { get; private set; }
    public string? ProviderPaymentId { get; private set; }
    public string? QrCode { get; private set; }
    public string? QrCodeImageUrl { get; private set; }
    public string? CopyPasteCode { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? PaidAt { get; private set; }
    public DateTimeOffset? CanceledAt { get; private set; }
    public DateTimeOffset? FailedAt { get; private set; }
    public string? FailureReason { get; private set; }

    private PixPayment() { }

    public static PixPayment CreatePending(
        Guid orderId,
        decimal amount,
        PixPaymentProviderType provider,
        string? providerPaymentId,
        string? qrCode,
        string? qrCodeImageUrl,
        string? copyPasteCode,
        DateTimeOffset? expiresAt)
    {
        if (orderId == Guid.Empty)
            throw new ArgumentException("Order id is required.", nameof(orderId));

        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Payment amount must be greater than zero.");

        return new PixPayment
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Amount = amount,
            Status = PixPaymentStatus.Pending,
            Provider = provider,
            ProviderPaymentId = providerPaymentId,
            QrCode = qrCode,
            QrCodeImageUrl = qrCodeImageUrl,
            CopyPasteCode = copyPasteCode,
            ExpiresAt = expiresAt,
            CreatedAt = DateTimeOffset.UtcNow,
            PaidAt = null,
            CanceledAt = null,
            FailedAt = null,
            FailureReason = null
        };
    }

    public void Expire()
    {
        if (Status == PixPaymentStatus.Expired)
            return;

        if (Status == PixPaymentStatus.Paid)
            return;

        if (Status == PixPaymentStatus.Canceled)
            return;

        if (Status != PixPaymentStatus.Pending)
            throw new InvalidOperationException(
                $"Pix payment {Id} cannot be expired because its status is {Status}.");

        Status = PixPaymentStatus.Expired;
    }
}
