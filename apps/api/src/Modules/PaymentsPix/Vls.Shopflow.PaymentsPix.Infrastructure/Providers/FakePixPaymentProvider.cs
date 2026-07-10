using Vls.Shopflow.PaymentsPix.Application.Interfaces;
using Vls.Shopflow.PaymentsPix.Domain.Enums;

namespace Vls.Shopflow.PaymentsPix.Infrastructure.Providers;

public sealed class FakePixPaymentProvider : IPixPaymentProvider
{
    public Task<PixChargeResponse> CreatePixChargeAsync(
        PixChargeRequest request,
        CancellationToken cancellationToken)
    {
        var response = new PixChargeResponse(
            PixPaymentProviderType.Fake,
            ProviderOrderId: $"fake-ord-{request.OrderId:N}",
            ProviderTransactionId: $"fake-pay-{request.OrderId:N}",
            QrCode: null,
            QrCodeImageUrl: null,
            CopyPasteCode: null,
            TicketUrl: null,
            ProviderStatus: "pending",
            ProviderStatusDetail: "fake",
            ProviderTransactionStatus: "pending",
            ProviderTransactionStatusDetail: "fake",
            ExternalReference: request.OrderId.ToString("D"),
            IdempotencyKey: request.OrderId.ToString("D"),
            ExpiresAt: request.ExpiresAt,
            Status: PixPaymentStatus.Pending);

        return Task.FromResult(response);
    }
}
