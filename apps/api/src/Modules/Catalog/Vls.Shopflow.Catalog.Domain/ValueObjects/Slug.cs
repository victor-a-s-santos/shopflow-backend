using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Vls.Shopflow.BuildingBlocks.Domain.ValueObjects;

namespace Vls.Shopflow.Catalog.Domain.ValueObjects;

public sealed record Slug : ValueObject
{
    private static readonly Regex _validSlugRegex =
        new("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.Compiled);

    public string Value { get; }

    private Slug(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Cria o Slug a partir de um nome (remove acentos, converte para minúsculas e substitui espaços por '-').
    /// </summary>
    public static Slug CreateFromName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Nome inválido para slug.");

        // 1. normaliza e remove acentos
        var normalized = name.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        var noAccents = sb.ToString();

        // 2. substitui espaços e separadores por '-'
        var slug = noAccents
            .ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("_", "-")
            .Replace("--", "-")
            .Trim('-');

        // 3. remove caracteres inválidos
        slug = Regex.Replace(slug, @"[^a-z0-9\-]", "");

        // 4. normaliza duplos traços
        while (slug.Contains("--"))
            slug = slug.Replace("--", "-");

        // 5. valida formato final
        if (!_validSlugRegex.IsMatch(slug))
            throw new ArgumentException($"Slug gerado '{slug}' é inválido.");

        return new Slug(slug);
    }

    /// <summary>
    /// Valida se o slug informado manualmente é válido.
    /// </summary>
    public static Slug From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Slug não pode ser vazio.");

        if (!_validSlugRegex.IsMatch(value))
            throw new ArgumentException("Slug inválido. Use letras minúsculas, números e traços.");

        return new Slug(value.Trim().ToLowerInvariant());
    }

    public override string ToString() => Value;

    // Implicit conversion para simplificar uso
    public static implicit operator string(Slug slug) => slug.Value;
}