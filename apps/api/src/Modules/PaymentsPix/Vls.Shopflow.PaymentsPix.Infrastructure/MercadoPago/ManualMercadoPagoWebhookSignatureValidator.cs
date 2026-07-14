using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Vls.Shopflow.PaymentsPix.Application.Interfaces;
using Vls.Shopflow.PaymentsPix.Application.Options;

namespace Vls.Shopflow.PaymentsPix.Infrastructure.MercadoPago;

/// <summary>
/// Manual HMAC validation (docs without SDK): alphanumerics lowercased in the manifest.
/// The official C# SDK (since case-preserve fix) includes data.id as received — use SDK as primary.
/// This class remains the diagnostic oracle for sdk_valid vs manual_valid divergence.
/// </summary>
public sealed class ManualMercadoPagoWebhookSignatureValidator(
    IOptions<MercadoPagoOptions> options)
{
    public MercadoPagoWebhookSignatureValidationResult Validate(
        string? xSignature,
        string? xRequestId,
        string? queryDataId,
        string? secret)
    {
        var hasXSignature = !string.IsNullOrWhiteSpace(xSignature);
        var hasXRequestId = !string.IsNullOrWhiteSpace(xRequestId);
        var hasQueryDataId = !string.IsNullOrWhiteSpace(queryDataId);
        var secretConfigured = !string.IsNullOrWhiteSpace(secret);

        string? dataIdRaw = hasQueryDataId ? queryDataId!.Trim() : null;
        string? dataIdForManifest = dataIdRaw is null ? null : NormalizeDataIdForManifest(dataIdRaw);
        var dataIdWasLowercased = dataIdRaw is not null
                                  && !string.Equals(dataIdRaw, dataIdForManifest, StringComparison.Ordinal);

        string? requestId = hasXRequestId ? xRequestId!.Trim() : null;

        if (!hasXSignature)
            return Fail(
                "Missing x-signature header.",
                "missing_signature",
                BuildDiagnostics(
                    hasXSignature, hasXRequestId, hasQueryDataId, dataIdWasLowercased,
                    tsPresent: false, v1Present: false, secretConfigured,
                    null, null, null, null,
                    BuildManifestPartsList(dataIdForManifest, requestId, tsIncluded: false),
                    MaskId(dataIdRaw), MaskId(requestId)));

        if (!secretConfigured)
            return Fail(
                "Webhook secret is not configured.",
                "missing_secret",
                BuildDiagnostics(
                    hasXSignature, hasXRequestId, hasQueryDataId, dataIdWasLowercased,
                    tsPresent: false, v1Present: false, secretConfigured: false,
                    null, null, null, null,
                    BuildManifestPartsList(dataIdForManifest, requestId, tsIncluded: false),
                    MaskId(dataIdRaw), MaskId(requestId)));

        if (!TryParseSignature(xSignature!, out var ts, out var v1))
        {
            var tsPresent = !string.IsNullOrWhiteSpace(ts);
            var v1Present = !string.IsNullOrWhiteSpace(v1);
            var code = !tsPresent ? "missing_ts" : !v1Present ? "missing_v1" : "invalid_signature_format";
            return Fail(
                "Invalid x-signature format.",
                code,
                BuildDiagnostics(
                    hasXSignature, hasXRequestId, hasQueryDataId, dataIdWasLowercased,
                    tsPresent, v1Present, secretConfigured,
                    null, null, Prefix(v1, 8), null,
                    BuildManifestPartsList(dataIdForManifest, requestId, tsPresent),
                    MaskId(dataIdRaw), MaskId(requestId)));
        }

        if (!TryParseSignedAt(ts, out var signedAt, out var ageSeconds))
        {
            return Fail(
                "Invalid signature timestamp.",
                "invalid_ts",
                BuildDiagnostics(
                    hasXSignature, hasXRequestId, hasQueryDataId, dataIdWasLowercased,
                    tsPresent: true, v1Present: true, secretConfigured,
                    null, null, Prefix(v1, 8), null,
                    BuildManifestPartsList(dataIdForManifest, requestId, tsIncluded: true),
                    MaskId(dataIdRaw), MaskId(requestId)));
        }

        var toleranceMinutes = Math.Max(1, options.Value.WebhookSignatureToleranceMinutes);
        var withinTolerance = Math.Abs(ageSeconds) <= TimeSpan.FromMinutes(toleranceMinutes).TotalSeconds;
        if (!withinTolerance)
        {
            return Fail(
                "Signature timestamp outside tolerance window.",
                "timestamp_out_of_tolerance",
                BuildDiagnostics(
                    hasXSignature, hasXRequestId, hasQueryDataId, dataIdWasLowercased,
                    tsPresent: true, v1Present: true, secretConfigured,
                    ageSeconds, false, Prefix(v1, 8), null,
                    BuildManifestPartsList(dataIdForManifest, requestId, tsIncluded: true),
                    MaskId(dataIdRaw), MaskId(requestId)));
        }

        var manifest = BuildManifest(dataIdForManifest, requestId, ts);
        var expected = ComputeHmacHex(secret!.Trim(), manifest);

        if (!FixedTimeEqualsHex(expected, v1))
        {
            return Fail(
                "Signature mismatch.",
                "signature_mismatch",
                BuildDiagnostics(
                    hasXSignature, hasXRequestId, hasQueryDataId, dataIdWasLowercased,
                    tsPresent: true, v1Present: true, secretConfigured,
                    ageSeconds, true, Prefix(v1, 8), Prefix(expected, 8),
                    BuildManifestPartsList(dataIdForManifest, requestId, tsIncluded: true),
                    MaskId(dataIdRaw), MaskId(requestId)));
        }

        return new MercadoPagoWebhookSignatureValidationResult(
            true,
            null,
            "ok",
            BuildDiagnostics(
                hasXSignature, hasXRequestId, hasQueryDataId, dataIdWasLowercased,
                tsPresent: true, v1Present: true, secretConfigured,
                ageSeconds, true, Prefix(v1, 8), Prefix(expected, 8),
                BuildManifestPartsList(dataIdForManifest, requestId, tsIncluded: true),
                MaskId(dataIdRaw), MaskId(requestId)));
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

    private static MercadoPagoWebhookSignatureValidationResult Fail(
        string reason,
        string code,
        MercadoPagoWebhookSignatureDiagnostics diagnostics)
        => new(false, reason, code, diagnostics with { FailureReasonCode = code });

    private static MercadoPagoWebhookSignatureDiagnostics BuildDiagnostics(
        bool hasXSignature,
        bool hasXRequestId,
        bool hasQueryDataId,
        bool dataIdQueryWasLowercased,
        bool tsPresent,
        bool v1Present,
        bool secretConfigured,
        long? timestampAgeSeconds,
        bool? timestampWithinTolerance,
        string? receivedV1Prefix,
        string? computedOfficialPrefix,
        string manifestPartsIncluded,
        string? queryDataIdMasked,
        string? requestIdMasked)
        => new(
            hasXSignature,
            hasXRequestId,
            hasQueryDataId,
            dataIdQueryWasLowercased,
            tsPresent,
            v1Present,
            secretConfigured,
            timestampAgeSeconds,
            timestampWithinTolerance,
            receivedV1Prefix,
            computedOfficialPrefix,
            manifestPartsIncluded,
            queryDataIdMasked,
            requestIdMasked,
            FailureReasonCode: "pending");

    /// <summary>
    /// Official rule: alphanumeric data.id with uppercase must be lowercased in the manifest.
    /// Safe to always ToLowerInvariant (numeric ids are unchanged).
    /// </summary>
    internal static string NormalizeDataIdForManifest(string dataId)
        => dataId.Trim().ToLowerInvariant();

    /// <summary>
    /// Builds official manifest. Omit id / request-id parts when those values are absent.
    /// Always ends with ';'. dataId should already be normalized (lowercase) when present.
    /// </summary>
    internal static string BuildManifest(string? dataIdNormalizedOrNull, string? requestIdOrNull, string ts)
    {
        var parts = new List<string>(3);
        if (!string.IsNullOrWhiteSpace(dataIdNormalizedOrNull))
            parts.Add($"id:{dataIdNormalizedOrNull.Trim()}");
        if (!string.IsNullOrWhiteSpace(requestIdOrNull))
            parts.Add($"request-id:{requestIdOrNull.Trim()}");
        parts.Add($"ts:{ts}");
        return string.Join(";", parts) + ";";
    }

    /// <summary>Convenience: normalizes raw query data.id then builds manifest.</summary>
    internal static string BuildManifestFromRaw(string dataId, string requestId, string ts)
        => BuildManifest(NormalizeDataIdForManifest(dataId), requestId, ts);

    private static string BuildManifestPartsList(string? dataId, string? requestId, bool tsIncluded)
    {
        var parts = new List<string>(3);
        if (!string.IsNullOrWhiteSpace(dataId))
            parts.Add("id");
        if (!string.IsNullOrWhiteSpace(requestId))
            parts.Add("request-id");
        if (tsIncluded)
            parts.Add("ts");
        return string.Join("/", parts);
    }

    internal static bool TryParseSignature(string xSignature, out string ts, out string v1)
    {
        ts = string.Empty;
        v1 = string.Empty;

        foreach (var part in xSignature.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var kv = part.Split('=', 2, StringSplitOptions.TrimEntries);
            if (kv.Length != 2)
                continue;

            if (kv[0].Equals("ts", StringComparison.OrdinalIgnoreCase))
                ts = kv[1];
            else if (kv[0].Equals("v1", StringComparison.OrdinalIgnoreCase))
                v1 = kv[1];
        }

        return !string.IsNullOrWhiteSpace(ts) && !string.IsNullOrWhiteSpace(v1);
    }

    /// <summary>
    /// Official Orders docs describe ts as milliseconds; some payment examples use seconds.
    /// Values with 13+ digits (or &gt; 1e12) are treated as Unix milliseconds.
    /// </summary>
    internal static bool TryParseSignedAt(string ts, out DateTimeOffset signedAt, out long ageSeconds)
    {
        signedAt = default;
        ageSeconds = 0;

        if (!long.TryParse(ts, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tsUnix))
            return false;

        try
        {
            signedAt = ts.Length >= 13 || tsUnix > 9_999_999_999L
                ? DateTimeOffset.FromUnixTimeMilliseconds(tsUnix)
                : DateTimeOffset.FromUnixTimeSeconds(tsUnix);
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }

        ageSeconds = (long)(DateTimeOffset.UtcNow - signedAt).TotalSeconds;
        return true;
    }

    internal static string ComputeHmacHex(string secret, string manifest)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var data = Encoding.UTF8.GetBytes(manifest);
        var hash = HMACSHA256.HashData(key, data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    internal static bool FixedTimeEqualsHex(string expectedHex, string actualHex)
    {
        if (expectedHex.Length != actualHex.Length)
            return false;

        try
        {
            var expected = Convert.FromHexString(expectedHex);
            var actual = Convert.FromHexString(actualHex);
            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    internal static string? MaskId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        if (trimmed.Length <= 10)
            return "***";

        return $"{trimmed[..6]}…{trimmed[^4..]}";
    }

    private static string? Prefix(string? value, int length)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        return value.Length <= length ? value : value[..length];
    }
}
