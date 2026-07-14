using MercadoPago.Webhook;
using Vls.Shopflow.PaymentsPix.Application.Interfaces;

namespace Vls.Shopflow.PaymentsPix.Infrastructure.MercadoPago;

/// <summary>
/// Adapter over <see cref="WebhookSignatureValidator"/> (mercadopago-sdk).
/// Passes query data.id as received (SDK does not lowercase).
/// </summary>
public sealed class MercadoPagoOfficialWebhookSignatureClient : IMercadoPagoOfficialWebhookSignatureClient
{
    public void Validate(
        string xSignature,
        string? xRequestId,
        string? queryDataId,
        string secret,
        TimeSpan? tolerance)
    {
        WebhookSignatureValidator.Validate(
            xSignature: xSignature,
            xRequestId: xRequestId ?? string.Empty,
            dataId: queryDataId ?? string.Empty,
            secret: secret,
            tolerance: tolerance);
    }
}
