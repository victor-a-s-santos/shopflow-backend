using Vls.Shopflow.PaymentsPix.Domain.Enums;

namespace Vls.Shopflow.PaymentsPix.Application.Interfaces;

public sealed record PixChargeRequest(
    Guid OrderId,
    decimal Amount,
    string CustomerName,
    string CustomerEmail,
    DateTimeOffset ExpiresAt);

public sealed record PixChargeResponse(
    PixPaymentProviderType Provider,
    string? ProviderOrderId,
    string? ProviderTransactionId,
    string? QrCode,
    string? QrCodeImageUrl,
    string? CopyPasteCode,
    string? TicketUrl,
    string? ProviderStatus,
    string? ProviderStatusDetail,
    string? ProviderTransactionStatus,
    string? ProviderTransactionStatusDetail,
    string? ExternalReference,
    string? IdempotencyKey,
    DateTimeOffset? ExpiresAt,
    PixPaymentStatus Status);

public interface IPixPaymentProvider
{
    Task<PixChargeResponse> CreatePixChargeAsync(
        PixChargeRequest request,
        CancellationToken cancellationToken);
}
