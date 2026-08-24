using MercadoPago.Error;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vls.Shopflow.PaymentsPix.Application.Interfaces;
using Vls.Shopflow.PaymentsPix.Application.Options;
using Vls.Shopflow.PaymentsPix.Application.Security;

namespace Vls.Shopflow.PaymentsPix.Infrastructure.MercadoPago;

/// <summary>
/// Primary: manual HMAC that reproduces Mercado Pago's official "Without SDKs" algorithm
/// (alphanumeric <c>data.id</c> lowercased in the manifest).
/// Diagnostic: mercadopago-sdk <c>WebhookSignatureValidator</c> (preserves data.id case since 3.x fix).
/// Final decision prefers the official manual algorithm when it validates; SDK alone never overrides a
/// manual rejection. SDK acceptance while manual rejects is logged as divergence (case-preserve bug path).
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
                sdkValid = null;
                sdkExceptionType = ex.GetType().Name;
                sdkFailure = "sdk_unavailable";
                logger.LogWarning(
                    "Mercado Pago SDK webhook validator unavailable ({SdkExceptionType}); " +
                    "manual official algorithm remains source of truth. " +
                    "manual_valid={ManualValid} secret_configured={SecretConfigured} webhook_secret_fingerprint={Fingerprint}",
                    sdkExceptionType,
                    manualValid,
                    secretConfigured,
                    fingerprint);
            }
        }

        // Decision: official manual algorithm is source of truth (docs "Without SDKs" + vector tests).
        // SDK is diagnostic / secondary agreement signal — never reject a valid official HMAC solely because SDK failed.
        bool isValid;
        string final;
        string failureCode;
        string? failureReason;

        if (manualValid)
        {
            isValid = true;
            final = "ManualOfficial";
            failureCode = "ok";
            failureReason = null;

            if (sdkValid == false)
            {
                logger.LogWarning(
                    "Mercado Pago webhook signature: official manual accepted but SDK rejected — accepting (manual SoT). " +
                    "Likely SDK case-preserve vs official lowercase data.id for ORD* ids. " +
                    "sdk_signature_valid=false manual_signature_valid=true " +
                    "received_v1_prefix={ReceivedV1} computed_official_prefix={ComputedOfficial} " +
                    "data_id_query_was_lowercased={Lowercased} sdk_exception_type={SdkExceptionType} " +
                    "sdk_failure={SdkFailure} query_data_id_masked={QueryMasked} " +
                    "webhook_secret_fingerprint={Fingerprint}",
                    manual.Diagnostics.ReceivedV1Prefix,
                    manual.Diagnostics.ComputedOfficialPrefix,
                    dataIdWouldBeLowercased,
                    sdkExceptionType,
                    sdkFailure,
                    Mask(queryTrimmed),
                    fingerprint);
            }
            else if (sdkValid == true)
            {
                // Both agree — keep ManualOfficial as final authority label; log at debug only via structured fields.
            }
        }
        else if (sdkValid == true)
        {
            // Manual rejected but SDK accepted — do NOT blindly trust SDK (case-preserve can accept forged casing).
            // Accept only when divergence is the known inverse case: raw uppercase id signed without lowercase
            // would make SDK pass and manual fail. That path is insecure relative to official docs; reject
            // unless prefixes show we cannot verify manual (missing). Prefer reject + log for investigation.
            isValid = false;
            final = "Rejected";
            failureCode = manual.FailureReasonCode;
            failureReason =
                "Official manual HMAC rejected while SDK accepted; rejecting per official lowercase algorithm.";
            logger.LogWarning(
                "Mercado Pago webhook signature: SDK accepted but official manual rejected — rejecting (manual SoT). " +
                "sdk_signature_valid=true manual_signature_valid=false " +
                "manual_failure_reason={ManualFailure} query_data_id_masked={QueryMasked} " +
                "data_id_query_was_lowercased={Lowercased} received_v1_prefix={ReceivedV1} " +
                "computed_official_prefix={ComputedOfficial} webhook_secret_fingerprint={Fingerprint} " +
                "secret_length={SecretLength} secret_trimmed_changed={SecretTrimmedChanged}",
                manual.FailureReasonCode,
                Mask(queryTrimmed),
                dataIdWouldBeLowercased,
                manual.Diagnostics.ReceivedV1Prefix,
                manual.Diagnostics.ComputedOfficialPrefix,
                fingerprint,
                secretLength,
                secretTrimmedChanged);
        }
        else
        {
            isValid = false;
            final = "Rejected";
            failureCode = manual.FailureReasonCode;
            failureReason = manual.FailureReason ?? sdkFailure ?? "Signature validation failed.";
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
