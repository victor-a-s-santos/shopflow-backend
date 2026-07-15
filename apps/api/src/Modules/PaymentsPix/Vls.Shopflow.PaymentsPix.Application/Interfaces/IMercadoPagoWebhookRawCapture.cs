namespace Vls.Shopflow.PaymentsPix.Application.Interfaces;

/// <summary>
/// TEMPORARY DIAGNOSTIC ONLY — raw Mercado Pago webhook capture for Testing/HML.
/// Must never emit secrets; must never run in Production.
/// </summary>
public interface IMercadoPagoWebhookRawCapture
{
    void TryCapture(
        MercadoPagoWebhookRawCaptureInput input,
        MercadoPagoWebhookSignatureValidationResult signatureResult);
}

public sealed record MercadoPagoWebhookRawCaptureInput(
    DateTimeOffset ReceivedAt,
    string RequestMethod,
    string RequestPath,
    string? RawQueryString,
    string? QueryDataIdExact,
    string? QueryTypeExact,
    string? HeaderXRequestIdExact,
    string? HeaderXSignatureExact,
    string? BodyRawJson,
    string? BodyApplicationId,
    string? BodyUserId,
    bool BodyLiveMode,
    string? BodyType,
    string? BodyAction,
    string? BodyDataId,
    string? BodyDataStatus,
    string? BodyDataStatusDetail);

/// <summary>Local signature probe (no public endpoint). Does not log secrets.</summary>
public interface IMercadoPagoWebhookSignatureProbe
{
    MercadoPagoWebhookSignatureProbeResult Probe(
        string? xSignature,
        string? xRequestId,
        string? queryDataId,
        string? secret);
}

public sealed record MercadoPagoWebhookSignatureProbeResult(
    bool? SdkValid,
    bool? ManualValid,
    string? SdkExceptionType,
    string? ManualFailureReason,
    string? SecretFingerprint,
    string SignatureValidatorFinal,
    bool IsValid);
