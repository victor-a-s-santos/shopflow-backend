using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vls.Shopflow.PaymentsPix.Application.Interfaces;
using Vls.Shopflow.PaymentsPix.Application.Options;
using Vls.Shopflow.PaymentsPix.Application.Security;

namespace Vls.Shopflow.PaymentsPix.Infrastructure.MercadoPago;

/// <summary>
/// TEMPORARY DIAGNOSTIC ONLY.
/// Captures webhook raw fields (including x-signature) solely in non-Production with an explicit flag
/// so SDK signature mismatches can be reproduced locally. Remove after diagnosis.
/// Mercado Pago does NOT send WebhookSecret — only x-signature; secret stays in env/panel.
/// </summary>
public sealed class MercadoPagoWebhookRawCapture(
    IHostEnvironment hostEnvironment,
    IOptions<MercadoPagoOptions> options,
    ILogger<MercadoPagoWebhookRawCapture> logger)
    : IMercadoPagoWebhookRawCapture
{
    public const int MaxBodyJsonChars = 8 * 1024;
    public const string CaptureLogMessage = "MP_WEBHOOK_RAW_CAPTURE {@Capture}";

    private int _captureCount;

    // Tracks which filtered order ids were already captured (separate from global max).
    private readonly ConcurrentDictionary<string, byte> _capturedOrderIds = new(StringComparer.OrdinalIgnoreCase);

    public void TryCapture(
        MercadoPagoWebhookRawCaptureInput input,
        MercadoPagoWebhookSignatureValidationResult signatureResult)
    {
        // Hard gate: never in Production, even if env mis-set.
        if (hostEnvironment.IsProduction())
            return;

        var opts = options.Value;
        if (!opts.WebhookRawCaptureEnabled)
            return;

        var queryDataId = input.QueryDataIdExact?.Trim();
        var filterOrderId = string.IsNullOrWhiteSpace(opts.WebhookRawCaptureOrderId)
            ? null
            : opts.WebhookRawCaptureOrderId.Trim();

        if (filterOrderId is not null)
        {
            if (string.IsNullOrWhiteSpace(queryDataId)
                || !string.Equals(queryDataId, filterOrderId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Capture at most once per filtered order id in this process.
            if (!_capturedOrderIds.TryAdd(queryDataId, 0))
                return;
        }
        else
        {
            var max = opts.WebhookRawCaptureMaxEvents <= 0 ? 5 : opts.WebhookRawCaptureMaxEvents;
            var n = Interlocked.Increment(ref _captureCount);
            if (n > max)
            {
                Interlocked.Decrement(ref _captureCount);
                return;
            }
        }

        ManualMercadoPagoWebhookSignatureValidator.TryParseSignature(
            input.HeaderXSignatureExact ?? string.Empty,
            out var parsedTs,
            out var parsedV1);

        var rawSecret = opts.WebhookSecret ?? string.Empty;
        var trimmedSecret = rawSecret.Trim();
        var secretTrimmedChanged = !string.Equals(rawSecret, trimmedSecret, StringComparison.Ordinal);
        var fingerprint = MercadoPagoSecretFingerprint.Compute(trimmedSecret);
        var secretLength = string.IsNullOrWhiteSpace(trimmedSecret) ? 0 : trimmedSecret.Length;

        var configuredAppId = NullIfWhiteSpace(opts.ApplicationId);
        var configuredUserId = NullIfWhiteSpace(opts.UserId);
        var bodyAppId = NullIfWhiteSpace(input.BodyApplicationId);
        var bodyUserId = NullIfWhiteSpace(input.BodyUserId);

        var d = signatureResult.Diagnostics;

        // TEMPORARY DIAGNOSTIC ONLY — includes x-signature for local SDK reproduction; never Production.
        var capture = new
        {
            received_at = input.ReceivedAt,
            request_method = input.RequestMethod,
            request_path = input.RequestPath,
            raw_query_string = Truncate(input.RawQueryString, 2048),
            query_data_id_exact = queryDataId,
            query_type_exact = input.QueryTypeExact,
            header_x_request_id_exact = input.HeaderXRequestIdExact,
            // TEMPORARY DIAGNOSTIC ONLY: x-signature is not the WebhookSecret; required to reproduce SDK Validate locally.
            header_x_signature_exact = input.HeaderXSignatureExact,
            parsed_ts = string.IsNullOrEmpty(parsedTs) ? null : parsedTs,
            parsed_v1 = string.IsNullOrEmpty(parsedV1) ? null : parsedV1,
            body_raw_json = Truncate(input.BodyRawJson, MaxBodyJsonChars),
            body_application_id = bodyAppId,
            body_user_id = bodyUserId,
            body_live_mode = input.BodyLiveMode,
            body_type = input.BodyType,
            body_action = input.BodyAction,
            body_data_id = input.BodyDataId,
            body_data_status = input.BodyDataStatus,
            body_data_status_detail = input.BodyDataStatusDetail,
            configured_application_id = configuredAppId,
            configured_user_id = configuredUserId,
            application_id_matches_config = IdsMatch(bodyAppId, configuredAppId),
            user_id_matches_config = IdsMatch(bodyUserId, configuredUserId),
            configured_environment = opts.Environment,
            webhook_secret_fingerprint = fingerprint,
            webhook_secret_length = secretLength,
            secret_trimmed_changed = secretTrimmedChanged,
            sdk_signature_valid = d.SdkSignatureValid,
            sdk_exception_type = d.SdkExceptionType,
            manual_signature_valid = d.ManualSignatureValid,
            manual_failure_reason = d.ManualFailureReason,
            signature_validator_final = d.SignatureValidatorFinal,
            aspnetcore_environment = hostEnvironment.EnvironmentName
        };

        logger.LogWarning(CaptureLogMessage, capture);
    }

    private static string? Truncate(string? value, int maxChars)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        return value.Length <= maxChars ? value : value[..maxChars] + "…(truncated)";
    }

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool? IdsMatch(string? left, string? right)
    {
        if (left is null || right is null)
            return null;

        return string.Equals(left, right, StringComparison.Ordinal);
    }
}
