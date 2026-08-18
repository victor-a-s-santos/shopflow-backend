using System.Text.RegularExpressions;

namespace Vls.Shopflow.Shipping.Application.Services;

public static class BrazilPostalCodeNormalizer
{
    private static readonly Regex DigitsOnly = new(@"^\d{8}$", RegexOptions.Compiled);

    /// <summary>
    /// Strips non-digits. Returns 8-digit CEP or null when invalid.
    /// </summary>
    public static string? TryNormalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var digits = new string(raw.Where(char.IsDigit).ToArray());
        return DigitsOnly.IsMatch(digits) ? digits : null;
    }

    public static string FormatMasked(string eightDigits)
        => $"{eightDigits[..5]}-{eightDigits[5..]}";
}
