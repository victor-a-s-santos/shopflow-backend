using System.Security.Cryptography;
using System.Text;

namespace Vls.Shopflow.PaymentsPix.Application.Security;

/// <summary>
/// Safe, non-reversible fingerprint for comparing configured Mercado Pago secrets across
/// environments without logging the secret itself.
/// </summary>
public static class MercadoPagoSecretFingerprint
{
    /// <summary>Returns first 8 hex chars of SHA256(UTF8(secret)), or null if secret empty.</summary>
    public static string? Compute(string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
            return null;

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(secret.Trim()));
        return Convert.ToHexString(hash)[..8].ToLowerInvariant();
    }
}
