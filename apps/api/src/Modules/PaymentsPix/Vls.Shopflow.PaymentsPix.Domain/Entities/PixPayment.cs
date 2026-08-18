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
    public string? ProviderOrderId { get; private set; }
    public string? ProviderTransactionId { get; private set; }
    public string? ProviderStatus { get; private set; }
    public string? ProviderStatusDetail { get; private set; }
    public string? ProviderTransactionStatus { get; private set; }
    public string? ProviderTransactionStatusDetail { get; private set; }
    public string? ExternalReference { get; private set; }
    public string? IdempotencyKey { get; private set; }
    public string? QrCode { get; private set; }
    public string? QrCodeImageUrl { get; private set; }
    public string? CopyPasteCode { get; private set; }
    public string? TicketUrl { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? PaidAt { get; private set; }
    public DateTimeOffset? ProviderApprovedAt { get; private set; }
    public DateTimeOffset? ProviderUpdatedAt { get; private set; }
    public DateTimeOffset? CanceledAt { get; private set; }
    public DateTimeOffset? FailedAt { get; private set; }
    public string? FailureReason { get; private set; }

    private PixPayment() { }

    public static PixPayment CreatePending(
        Guid orderId,
        decimal amount,
        PixPaymentProviderType provider,
        string? providerOrderId,
        string? providerTransactionId,
        string? qrCode,
        string? qrCodeImageUrl,
        string? copyPasteCode,
        string? ticketUrl,
        string? providerStatus,
        string? providerStatusDetail,
        string? providerTransactionStatus,
        string? providerTransactionStatusDetail,
        string? externalReference,
        string? idempotencyKey,
        DateTimeOffset? expiresAt)
    {
        if (orderId == Guid.Empty)
            throw new ArgumentException("Order id is required.", nameof(orderId));

        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Payment amount must be greater than zero.");

        var transactionId = Normalize(providerTransactionId);
        var orderProviderId = Normalize(providerOrderId);

        return new PixPayment
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Amount = amount,
            Status = PixPaymentStatus.Pending,
            Provider = provider,
            ProviderOrderId = orderProviderId,
            ProviderTransactionId = transactionId,
            // Keep ProviderPaymentId aligned with transaction id for legacy lookups.
            ProviderPaymentId = transactionId ?? orderProviderId,
            ProviderStatus = Truncate(providerStatus, 50),
            ProviderStatusDetail = Truncate(providerStatusDetail, 100),
            ProviderTransactionStatus = Truncate(providerTransactionStatus, 50),
            ProviderTransactionStatusDetail = Truncate(providerTransactionStatusDetail, 100),
            ExternalReference = Truncate(externalReference, 150),
            IdempotencyKey = Truncate(idempotencyKey, 64),
            QrCode = qrCode,
            QrCodeImageUrl = qrCodeImageUrl,
            CopyPasteCode = copyPasteCode,
            TicketUrl = ticketUrl,
            ExpiresAt = expiresAt,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void MarkAsPaid(
        string? providerStatus,
        string? providerStatusDetail,
        string? providerTransactionStatus,
        string? providerTransactionStatusDetail,
        DateTimeOffset? approvedAt,
        string? providerOrderId = null,
        string? providerTransactionId = null)
    {
        if (Status == PixPaymentStatus.Paid)
        {
            UpdateProviderMetadata(
                providerStatus,
                providerStatusDetail,
                providerTransactionStatus,
                providerTransactionStatusDetail,
                approvedAt,
                providerOrderId,
                providerTransactionId);
            return;
        }

        if (Status != PixPaymentStatus.Pending)
            throw new InvalidOperationException(
                $"Pix payment {Id} cannot be marked as Paid because its status is {Status}.");

        Status = PixPaymentStatus.Paid;
        PaidAt = approvedAt ?? DateTimeOffset.UtcNow;
        UpdateProviderMetadata(
            providerStatus,
            providerStatusDetail,
            providerTransactionStatus,
            providerTransactionStatusDetail,
            approvedAt,
            providerOrderId,
            providerTransactionId);
    }

    public void MarkAsFailed(
        string? providerStatus,
        string? providerStatusDetail,
        string? reason,
        string? providerTransactionStatus = null,
        string? providerTransactionStatusDetail = null)
    {
        if (Status is PixPaymentStatus.Failed or PixPaymentStatus.Paid or PixPaymentStatus.Canceled or PixPaymentStatus.Expired)
        {
            UpdateProviderMetadata(providerStatus, providerStatusDetail, providerTransactionStatus, providerTransactionStatusDetail, null, null, null);
            return;
        }

        if (Status != PixPaymentStatus.Pending)
            throw new InvalidOperationException(
                $"Pix payment {Id} cannot be marked as Failed because its status is {Status}.");

        Status = PixPaymentStatus.Failed;
        FailedAt = DateTimeOffset.UtcNow;
        FailureReason = Truncate(reason, 500);
        UpdateProviderMetadata(providerStatus, providerStatusDetail, providerTransactionStatus, providerTransactionStatusDetail, null, null, null);
    }

    public void MarkAsCanceled(
        string? providerStatus,
        string? providerStatusDetail,
        string? reason,
        string? providerTransactionStatus = null,
        string? providerTransactionStatusDetail = null)
    {
        if (Status is PixPaymentStatus.Canceled or PixPaymentStatus.Paid)
        {
            UpdateProviderMetadata(providerStatus, providerStatusDetail, providerTransactionStatus, providerTransactionStatusDetail, null, null, null);
            return;
        }

        if (Status != PixPaymentStatus.Pending)
            throw new InvalidOperationException(
                $"Pix payment {Id} cannot be marked as Canceled because its status is {Status}.");

        Status = PixPaymentStatus.Canceled;
        CanceledAt = DateTimeOffset.UtcNow;
        FailureReason = Truncate(reason, 500);
        UpdateProviderMetadata(providerStatus, providerStatusDetail, providerTransactionStatus, providerTransactionStatusDetail, null, null, null);
    }

    public void UpdateProviderStatus(
        string? providerStatus,
        string? providerStatusDetail,
        string? providerTransactionStatus = null,
        string? providerTransactionStatusDetail = null)
    {
        UpdateProviderMetadata(
            providerStatus,
            providerStatusDetail,
            providerTransactionStatus,
            providerTransactionStatusDetail,
            null,
            null,
            null);
    }

    public void SyncProviderIds(string? providerOrderId, string? providerTransactionId)
    {
        if (!string.IsNullOrWhiteSpace(providerOrderId))
            ProviderOrderId = Normalize(providerOrderId);

        if (!string.IsNullOrWhiteSpace(providerTransactionId))
        {
            ProviderTransactionId = Normalize(providerTransactionId);
            ProviderPaymentId = ProviderTransactionId;
        }
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

    private void UpdateProviderMetadata(
        string? providerStatus,
        string? providerStatusDetail,
        string? providerTransactionStatus,
        string? providerTransactionStatusDetail,
        DateTimeOffset? approvedAt,
        string? providerOrderId,
        string? providerTransactionId)
    {
        if (!string.IsNullOrWhiteSpace(providerStatus))
            ProviderStatus = Truncate(providerStatus, 50);

        if (!string.IsNullOrWhiteSpace(providerStatusDetail))
            ProviderStatusDetail = Truncate(providerStatusDetail, 100);

        if (!string.IsNullOrWhiteSpace(providerTransactionStatus))
            ProviderTransactionStatus = Truncate(providerTransactionStatus, 50);

        if (!string.IsNullOrWhiteSpace(providerTransactionStatusDetail))
            ProviderTransactionStatusDetail = Truncate(providerTransactionStatusDetail, 100);

        if (approvedAt.HasValue)
            ProviderApprovedAt = approvedAt;

        ProviderUpdatedAt = DateTimeOffset.UtcNow;
        SyncProviderIds(providerOrderId, providerTransactionId);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
