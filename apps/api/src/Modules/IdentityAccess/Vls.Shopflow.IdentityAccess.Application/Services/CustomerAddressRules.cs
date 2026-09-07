using System.Text.RegularExpressions;

namespace Vls.Shopflow.IdentityAccess.Application.Services;

public static class CustomerAddressRules
{
    private static readonly Regex UfRegex = new("^[A-Z]{2}$", RegexOptions.Compiled);

    public static string? TryNormalizePostalCode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        return digits.Length == 8 ? digits : null;
    }

    public static string FormatPostalCode(string eightDigits)
        => $"{eightDigits[..5]}-{eightDigits[5..]}";

    public static IReadOnlyDictionary<string, string[]> Validate(
        string? label,
        string? recipientName,
        string? postalCode,
        string? street,
        string? number,
        string? complement,
        string? neighborhood,
        string? city,
        string? state)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(label) && label.Trim().Length > 40)
            Add(errors, "label", "O apelido deve ter no máximo 40 caracteres.");

        if (string.IsNullOrWhiteSpace(recipientName))
            Add(errors, "recipientName", "Informe o nome do destinatário.");
        else if (recipientName.Trim().Length > 200)
            Add(errors, "recipientName", "O nome do destinatário deve ter no máximo 200 caracteres.");

        if (TryNormalizePostalCode(postalCode) is null)
            Add(errors, "postalCode", "Informe um CEP brasileiro com 8 dígitos.");

        if (string.IsNullOrWhiteSpace(street))
            Add(errors, "street", "Informe a rua.");
        else if (street.Trim().Length > 200)
            Add(errors, "street", "A rua deve ter no máximo 200 caracteres.");

        if (string.IsNullOrWhiteSpace(number))
            Add(errors, "number", "Informe o número ou S/N.");
        else if (number.Trim().Length > 30)
            Add(errors, "number", "O número deve ter no máximo 30 caracteres.");

        if (!string.IsNullOrWhiteSpace(complement) && complement.Trim().Length > 120)
            Add(errors, "complement", "O complemento deve ter no máximo 120 caracteres.");

        if (string.IsNullOrWhiteSpace(neighborhood))
            Add(errors, "neighborhood", "Informe o bairro.");
        else if (neighborhood.Trim().Length > 120)
            Add(errors, "neighborhood", "O bairro deve ter no máximo 120 caracteres.");

        if (string.IsNullOrWhiteSpace(city))
            Add(errors, "city", "Informe a cidade.");
        else if (city.Trim().Length > 120)
            Add(errors, "city", "A cidade deve ter no máximo 120 caracteres.");

        var uf = state?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(uf) || !UfRegex.IsMatch(uf))
            Add(errors, "state", "Informe a UF com 2 letras.");

        return errors.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.ToArray(),
            StringComparer.OrdinalIgnoreCase);
    }

    private static void Add(Dictionary<string, List<string>> errors, string field, string message)
    {
        if (!errors.TryGetValue(field, out var list))
        {
            list = [];
            errors[field] = list;
        }

        list.Add(message);
    }
}
