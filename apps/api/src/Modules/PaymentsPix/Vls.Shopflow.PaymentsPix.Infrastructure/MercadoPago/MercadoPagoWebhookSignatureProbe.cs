using Vls.Shopflow.PaymentsPix.Application.Interfaces;

namespace Vls.Shopflow.PaymentsPix.Infrastructure.MercadoPago;

/// <summary>
/// Internal probe wrapping the composite signature validator. No public HTTP endpoint.
/// </summary>
public sealed class MercadoPagoWebhookSignatureProbe(
    IMercadoPagoWebhookSignatureValidator signatureValidator)
    : IMercadoPagoWebhookSignatureProbe
{
    public MercadoPagoWebhookSignatureProbeResult Probe(
        string? xSignature,
        string? xRequestId,
        string? queryDataId,
        string? secret)
    {
        var result = signatureValidator.Validate(xSignature, xRequestId, queryDataId, secret);
        var d = result.Diagnostics;
        return new MercadoPagoWebhookSignatureProbeResult(
            d.SdkSignatureValid,
            d.ManualSignatureValid,
            d.SdkExceptionType,
            d.ManualFailureReason,
            d.WebhookSecretFingerprint,
            d.SignatureValidatorFinal,
            result.IsValid);
    }
}
