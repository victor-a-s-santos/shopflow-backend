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
            ProviderPaymentId: $"fake-dev-{request.OrderId:N}",
            QrCode: null,
            QrCodeImageUrl: null,
            CopyPasteCode: null,
            ExpiresAt: request.ExpiresAt,
            Status: PixPaymentStatus.Pending);

        return Task.FromResult(response);
    }
}
