using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Vls.Shopflow.Catalog.Application.Validations.Common;

namespace Vls.Shopflow.Catalog.Application.Services;

/// <summary>
/// Official SKU code rules: normalize informed codes; auto-generate unique readable codes when empty.
/// Uniqueness scope: per product (ProductId + Code), matching DB unique index.
/// </summary>
public static partial class SkuCodeGenerator
{
    private static readonly Regex NonCodeChars = NonCodeCharsRegex();

    public static string Normalize(string code)
    {
        var trimmed = code.Trim().ToUpperInvariant();
        trimmed = trimmed.Replace(' ', '-');
        trimmed = NonCodeChars.Replace(trimmed, "-");
        trimmed = CollapseDashes().Replace(trimmed, "-").Trim('-');

        if (trimmed.Length > CommonRules.MaxSkuCodeLen)
            trimmed = trimmed[..CommonRules.MaxSkuCodeLen].TrimEnd('-');

        return trimmed;
    }

    public static bool IsEmpty(string? code) => string.IsNullOrWhiteSpace(code);

    /// <summary>
    /// Builds a readable code from product name + attribute value labels.
    /// Falls back to SKU-{guid8} when nothing useful is available.
    /// Appends -2, -3… on collision within the product.
    /// </summary>
    public static string GenerateUnique(
        string productName,
        IEnumerable<string?> attributeValueLabels,
        IReadOnlySet<string> existingNormalizedCodes,
        Guid? skuIdHint = null)
    {
        var parts = new List<string>();
        var productPart = Slugify(productName);
        if (!string.IsNullOrEmpty(productPart))
            parts.Add(productPart);

        foreach (var label in attributeValueLabels)
        {
            var part = Slugify(label);
            if (!string.IsNullOrEmpty(part))
                parts.Add(part);
        }

        string baseCode;
        if (parts.Count == 0)
        {
            var hint = (skuIdHint ?? Guid.NewGuid()).ToString("N")[..8].ToUpperInvariant();
            baseCode = $"SKU-{hint}";
        }
        else
        {
            baseCode = string.Join('-', parts);
            if (baseCode.Length > CommonRules.MaxSkuCodeLen - 4)
                baseCode = baseCode[..(CommonRules.MaxSkuCodeLen - 4)].TrimEnd('-');
        }

        baseCode = Normalize(baseCode);
        if (string.IsNullOrEmpty(baseCode))
        {
            var hint = (skuIdHint ?? Guid.NewGuid()).ToString("N")[..8].ToUpperInvariant();
            baseCode = $"SKU-{hint}";
        }

        var candidate = baseCode;
        var suffix = 2;
        while (existingNormalizedCodes.Contains(candidate))
        {
            var suffixText = $"-{suffix.ToString(CultureInfo.InvariantCulture)}";
            var maxBase = CommonRules.MaxSkuCodeLen - suffixText.Length;
            var truncated = baseCode.Length > maxBase
                ? baseCode[..maxBase].TrimEnd('-')
                : baseCode;
            candidate = Normalize(truncated + suffixText);
            suffix++;
        }

        return candidate;
    }

    public static string Slugify(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category == UnicodeCategory.NonSpacingMark)
                continue;
            sb.Append(c);
        }

        var ascii = sb.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();
        ascii = NonCodeChars.Replace(ascii, "-");
        ascii = CollapseDashes().Replace(ascii, "-").Trim('-');
        return ascii;
    }

    [GeneratedRegex(@"[^A-Z0-9\-]+", RegexOptions.Compiled)]
    private static partial Regex NonCodeCharsRegex();

    [GeneratedRegex(@"-{2,}", RegexOptions.Compiled)]
    private static partial Regex CollapseDashes();
}
