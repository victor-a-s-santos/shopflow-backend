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
    string? ProviderPaymentId,
    string? QrCode,
    string? QrCodeImageUrl,
    string? CopyPasteCode,
    DateTimeOffset? ExpiresAt,
    PixPaymentStatus Status);

public interface IPixPaymentProvider
{
    Task<PixChargeResponse> CreatePixChargeAsync(
        PixChargeRequest request,
        CancellationToken cancellationToken);
}
