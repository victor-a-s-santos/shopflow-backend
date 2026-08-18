using System.Text;
using System.Text.RegularExpressions;

namespace Vls.Shopflow.Orders.Domain.Services;

/// <summary>
/// Normalizes contact fields for guest delivery-batch identity matching.
/// </summary>
public static class CustomerContactNormalizer
{
    private static readonly Regex NonDigits = new(@"\D+", RegexOptions.Compiled);

    public static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        return email.Trim().ToLowerInvariant();
    }

    public static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return null;

        var digits = NonDigits.Replace(phone, string.Empty);
        return digits.Length == 0 ? null : digits;
    }

    public static string AddressFingerprint(
        string zipCode,
        string street,
        string number,
        string? complement,
        string neighborhood,
        string city,
        string state)
    {
        var sb = new StringBuilder();
        Append(sb, zipCode);
        Append(sb, street);
        Append(sb, number);
        Append(sb, complement);
        Append(sb, neighborhood);
        Append(sb, city);
        Append(sb, state);
        return sb.ToString();
    }

    public static string AddressSummary(
        string city,
        string state,
        string zipCode)
    {
        var zip = string.IsNullOrWhiteSpace(zipCode) ? "" : zipCode.Trim();
        if (zip.Length == 8 && zip.All(char.IsDigit))
            zip = $"{zip[..5]}-{zip[5..]}";

        return $"{city.Trim()}/{state.Trim().ToUpperInvariant()} - CEP {zip}";
    }

    private static void Append(StringBuilder sb, string? value)
    {
        if (sb.Length > 0)
            sb.Append('|');
        sb.Append((value ?? string.Empty).Trim().ToUpperInvariant());
    }
}
