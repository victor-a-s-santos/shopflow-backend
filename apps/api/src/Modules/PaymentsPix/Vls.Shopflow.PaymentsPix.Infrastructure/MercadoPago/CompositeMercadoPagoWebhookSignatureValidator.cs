using MercadoPago.Error;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vls.Shopflow.PaymentsPix.Application.Interfaces;
using Vls.Shopflow.PaymentsPix.Application.Options;
using Vls.Shopflow.PaymentsPix.Application.Security;

namespace Vls.Shopflow.PaymentsPix.Infrastructure.MercadoPago;

/// <summary>
/// Primary: official mercadopago-sdk WebhookSignatureValidator.
/// Diagnostic: manual lowercase-manifest HMAC. Final decision prefers SDK.
/// </summary>
public sealed class CompositeMercadoPagoWebhookSignatureValidator(
    IMercadoPagoOfficialWebhookSignatureClient sdkClient,
    ManualMercadoPagoWebhookSignatureValidator manualValidator,
    IOptions<MercadoPagoOptions> options,
    ILogger<CompositeMercadoPagoWebhookSignatureValidator> logger)
    : IMercadoPagoWebhookSignatureValidator
{
    public MercadoPagoWebhookSignatureValidationResult Validate(
        string? xSignature,
        string? xRequestId,
        string? queryDataId,
        string? secret)
    {
        var rawSecret = secret ?? string.Empty;
        var trimmedSecret = rawSecret.Trim();
        var secretTrimmedChanged = !string.Equals(rawSecret, trimmedSecret, StringComparison.Ordinal);
        var secretConfigured = !string.IsNullOrWhiteSpace(trimmedSecret);
        var secretLength = secretConfigured ? trimmedSecret.Length : 0;
        var fingerprint = MercadoPagoSecretFingerprint.Compute(trimmedSecret);

        var hasXSignature = !string.IsNullOrWhiteSpace(xSignature);
        var hasXRequestId = !string.IsNullOrWhiteSpace(xRequestId);
        var hasQueryDataId = !string.IsNullOrWhiteSpace(queryDataId);
        var queryTrimmed = hasQueryDataId ? queryDataId!.Trim() : null;
        var dataIdWouldBeLowercased = queryTrimmed is not null
            && !string.Equals(queryTrimmed, queryTrimmed.ToLowerInvariant(), StringComparison.Ordinal);

        if (!secretConfigured)
        {
            return BuildResult(
                isValid: false,
                failureReason: "Webhook secret is not configured.",
                failureCode: "missing_secret",
                sdkValid: null,
                manualValid: null,
                final: "Rejected",
                sdkExceptionType: null,
                manualFailure: "missing_secret",
                hasXSignature, hasXRequestId, hasQueryDataId, dataIdWouldBeLowercased,
                secretConfigured, secretLength, secretTrimmedChanged, fingerprint,
                queryTrimmed, xRequestId);
        }

        var manual = manualValidator.Validate(xSignature, xRequestId, queryDataId, trimmedSecret);
        var manualValid = manual.IsValid;

        bool? sdkValid = null;
        string? sdkExceptionType = null;
        string? sdkFailure = null;

        if (!hasXSignature)
        {
            sdkValid = false;
            sdkFailure = "Missing x-signature header.";
            sdkExceptionType = nameof(InvalidWebhookSignatureException);
        }
        else
        {
            try
            {
                var toleranceMinutes = Math.Max(1, options.Value.WebhookSignatureToleranceMinutes);
                sdkClient.Validate(
                    xSignature!,
                    xRequestId,
                    queryDataId,
                    trimmedSecret,
                    TimeSpan.FromMinutes(toleranceMinutes));
                sdkValid = true;
            }
            catch (InvalidWebhookSignatureException ex)
            {
                sdkValid = false;
                sdkExceptionType = nameof(InvalidWebhookSignatureException);
                sdkFailure = ex.Reason.ToString();
            }
            catch (ArgumentNullException)
            {
                sdkValid = false;
                sdkExceptionType = nameof(ArgumentNullException);
                sdkFailure = "missing_secret";
            }
            catch (Exception ex)
            {
                // Unusual SDK failure — fall back to manual and document.
                sdkValid = null;
                sdkExceptionType = ex.GetType().Name;
                sdkFailure = "sdk_unavailable";
                logger.LogWarning(
                    "Mercado Pago SDK webhook validator unavailable ({SdkExceptionType}); using manual fallback. " +
                    "manual_valid={ManualValid} secret_configured={SecretConfigured} webhook_secret_fingerprint={Fingerprint}",
                    sdkExceptionType,
                    manualValid,
                    secretConfigured,
                    fingerprint);
            }
        }

        // Decision: SDK primary when available; manual only as fallback if SDK threw unexpected exception.
        bool isValid;
        string final;
        string failureCode;
        string? failureReason;

        if (sdkValid == true)
        {
            isValid = true;
            final = "Sdk";
            failureCode = "ok";
            failureReason = null;
            if (manualValid == false)
            {
                logger.LogWarning(
                    "Mercado Pago webhook signature: SDK accepted but manual lowercase-manifest rejected. " +
                    "Preferring SDK (data.id case preserve). " +
                    "sdk_signature_valid=true manual_signature_valid=false " +
                    "manual_failure_reason={ManualFailure} query_data_id_masked={QueryMasked} " +
                    "data_id_query_was_lowercased={Lowercased} webhook_secret_fingerprint={Fingerprint} " +
                    "secret_length={SecretLength} secret_trimmed_changed={SecretTrimmedChanged}",
                    manual.FailureReasonCode,
                    Mask(queryTrimmed),
                    dataIdWouldBeLowercased,
                    fingerprint,
                    secretLength,
                    secretTrimmedChanged);
            }
        }
        else if (sdkValid == false)
        {
            isValid = false;
            final = "Rejected";
            failureCode = MapSdkFailure(sdkFailure) ?? "signature_mismatch";
            failureReason = sdkFailure ?? "SDK signature validation failed.";
            if (manualValid)
            {
                logger.LogWarning(
                    "Mercado Pago webhook signature: SDK rejected but manual accepted — rejecting (SDK primary). " +
                    "sdk_signature_valid=false manual_signature_valid=true " +
                    "sdk_exception_type={SdkExceptionType} sdk_failure={SdkFailure} " +
                    "query_data_id_masked={QueryMasked} webhook_secret_fingerprint={Fingerprint}",
                    sdkExceptionType,
                    sdkFailure,
                    Mask(queryTrimmed),
                    fingerprint);
            }
        }
        else
        {
            // SDK unavailable → manual fallback.
            isValid = manualValid;
            final = manualValid ? "ManualFallback" : "Rejected";
            failureCode = manualValid ? "ok" : manual.FailureReasonCode;
            failureReason = manual.FailureReason;
        }

        var baseDiag = manual.Diagnostics;
        return new MercadoPagoWebhookSignatureValidationResult(
            isValid,
            failureReason,
            failureCode,
            baseDiag with
            {
                FailureReasonCode = failureCode,
                HasXSignature = hasXSignature,
                HasXRequestId = hasXRequestId,
                HasQueryDataId = hasQueryDataId,
                DataIdQueryWasLowercased = dataIdWouldBeLowercased,
                SecretConfigured = secretConfigured,
                SdkSignatureValid = sdkValid,
                ManualSignatureValid = manualValid,
                SignatureValidatorFinal = final,
                SdkExceptionType = sdkExceptionType,
                ManualFailureReason = manual.FailureReason ?? manual.FailureReasonCode,
                SecretLength = secretLength,
                SecretTrimmedChanged = secretTrimmedChanged,
                WebhookSecretFingerprint = fingerprint,
                QueryDataIdMasked = baseDiag.QueryDataIdMasked ?? Mask(queryTrimmed),
                RequestIdMasked = baseDiag.RequestIdMasked ?? Mask(xRequestId)
            });
    }

    public bool IsValid(
        string? xSignature,
        string? xRequestId,
        string dataId,
        string secret,
        out string? failureReason)
    {
        var result = Validate(xSignature, xRequestId, dataId, secret);
        failureReason = result.FailureReason;
        return result.IsValid;
    }

    private static MercadoPagoWebhookSignatureValidationResult BuildResult(
        bool isValid,
        string? failureReason,
        string failureCode,
        bool? sdkValid,
        bool? manualValid,
        string final,
        string? sdkExceptionType,
        string? manualFailure,
        bool hasXSignature,
        bool hasXRequestId,
        bool hasQueryDataId,
        bool dataIdWouldBeLowercased,
        bool secretConfigured,
        int secretLength,
        bool secretTrimmedChanged,
        string? fingerprint,
        string? queryDataId,
        string? xRequestId)
        => new(
            isValid,
            failureReason,
            failureCode,
            new MercadoPagoWebhookSignatureDiagnostics(
                hasXSignature,
                hasXRequestId,
                hasQueryDataId,
                dataIdWouldBeLowercased,
                TsPresent: false,
                V1Present: false,
                secretConfigured,
                TimestampAgeSeconds: null,
                TimestampWithinTolerance: null,
                ReceivedV1Prefix: null,
                ComputedOfficialPrefix: null,
                ManifestPartsIncluded: string.Empty,
                QueryDataIdMasked: Mask(queryDataId),
                RequestIdMasked: Mask(xRequestId),
                FailureReasonCode: failureCode,
                SdkSignatureValid: sdkValid,
                ManualSignatureValid: manualValid,
                SignatureValidatorFinal: final,
                SdkExceptionType: sdkExceptionType,
                ManualFailureReason: manualFailure,
                SecretLength: secretLength,
                SecretTrimmedChanged: secretTrimmedChanged,
                WebhookSecretFingerprint: fingerprint));

    private static string? MapSdkFailure(string? sdkFailure)
        => sdkFailure switch
        {
            nameof(SignatureFailureReason.MissingSignatureHeader) => "missing_signature",
            nameof(SignatureFailureReason.MissingTimestamp) => "missing_ts",
            nameof(SignatureFailureReason.MissingHash) => "missing_v1",
            nameof(SignatureFailureReason.TimestampOutOfTolerance) => "timestamp_out_of_tolerance",
            nameof(SignatureFailureReason.SignatureMismatch) => "signature_mismatch",
            nameof(SignatureFailureReason.MalformedSignatureHeader) => "invalid_signature_format",
            "missing_secret" => "missing_secret",
            _ => sdkFailure is null ? null : "signature_mismatch"
        };

    private static string? Mask(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        if (trimmed.Length <= 10)
            return "***";

        return $"{trimmed[..6]}…{trimmed[^4..]}";
    }
}
