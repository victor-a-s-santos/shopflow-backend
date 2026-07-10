using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Vls.Shopflow.PaymentsPix.Application.Interfaces;
using Vls.Shopflow.PaymentsPix.Application.Options;

namespace Vls.Shopflow.PaymentsPix.Infrastructure.MercadoPago;

public sealed class MercadoPagoWebhookSignatureValidator(
    IOptions<MercadoPagoOptions> options)
    : IMercadoPagoWebhookSignatureValidator
{
    public bool IsValid(
        string? xSignature,
        string? xRequestId,
        string dataId,
        string secret,
        out string? failureReason)
    {
        failureReason = null;

        if (string.IsNullOrWhiteSpace(xSignature))
        {
            failureReason = "Missing x-signature header.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(xRequestId))
        {
            failureReason = "Missing x-request-id header.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(dataId))
        {
            failureReason = "Missing data.id.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(secret))
        {
            failureReason = "Webhook secret is not configured.";
            return false;
        }

        if (!TryParseSignature(xSignature, out var ts, out var v1))
        {
            failureReason = "Invalid x-signature format.";
            return false;
        }

        var toleranceMinutes = Math.Max(1, options.Value.WebhookSignatureToleranceMinutes);
        if (!long.TryParse(ts, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tsUnix))
        {
            failureReason = "Invalid signature timestamp.";
            return false;
        }

        var signedAt = DateTimeOffset.FromUnixTimeSeconds(tsUnix);
        var age = DateTimeOffset.UtcNow - signedAt;
        if (age.Duration() > TimeSpan.FromMinutes(toleranceMinutes))
        {
            failureReason = "Signature timestamp outside tolerance window.";
            return false;
        }

        var manifestDataId = NormalizeDataIdForManifest(dataId);
        var manifest = $"id:{manifestDataId};request-id:{xRequestId};ts:{ts};";
        var expected = ComputeHmacHex(secret, manifest);

        if (!FixedTimeEqualsHex(expected, v1))
        {
            failureReason = "Signature mismatch.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Mercado Pago docs: alphanumeric data.id values with uppercase letters must be lowercased in the manifest.
    /// </summary>
    internal static string NormalizeDataIdForManifest(string dataId)
    {
        var trimmed = dataId.Trim();
        return trimmed.Any(char.IsLetter) ? trimmed.ToLowerInvariant() : trimmed;
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

    internal static string BuildManifest(string dataId, string requestId, string ts)
        => $"id:{NormalizeDataIdForManifest(dataId)};request-id:{requestId};ts:{ts};";

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
}
