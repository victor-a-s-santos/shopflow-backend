using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Vls.Shopflow.Catalog.Application.Services;

public static partial class ProductImageStorageKeys
{
    /// <summary>
    /// Admin upload key: {prefix}/{productId}/{imageId}-{slug}.{ext}
    /// </summary>
    public static string Build(
        string prefix,
        Guid productId,
        Guid imageId,
        string? slug,
        string extension)
    {
        var safePrefix = NormalizePrefix(prefix);
        var ext = NormalizeExtension(extension);
        var safeSlug = SanitizeSlug(slug);
        return $"{safePrefix}/{productId:D}/{imageId:N}-{safeSlug}{ext}";
    }

    /// <summary>
    /// Idempotent demo-seed key: {prefix}/seed/{productSlug}/{fileName}
    /// </summary>
    public static string BuildSeedKey(string prefix, string productSlug, string fileName)
    {
        var safePrefix = NormalizePrefix(prefix);
        var slug = SanitizeSlug(productSlug);
        var file = SanitizeFileName(fileName);
        return $"{safePrefix}/seed/{slug}/{file}";
    }

    public static string NormalizePrefix(string? prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            return "products";
        return prefix.Trim().Trim('/').Replace('\\', '/');
    }

    public static string NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return ".bin";

        var ext = extension.Trim().ToLowerInvariant();
        if (!ext.StartsWith('.'))
            ext = "." + ext;

        return ext is ".png" or ".jpg" or ".jpeg" or ".webp"
            ? (ext == ".jpeg" ? ".jpg" : ext)
            : ".bin";
    }

    public static string SanitizeSlug(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "image";

        var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
                continue;
            if (ch is >= 'a' and <= 'z' or >= '0' and <= '9')
                sb.Append(ch);
            else if (ch is '-' or '_' or ' ')
                sb.Append('-');
        }

        var slug = Regex.Replace(sb.ToString(), "-{2,}", "-").Trim('-');
        if (slug.Length > 48)
            slug = slug[..48].Trim('-');

        return string.IsNullOrWhiteSpace(slug) ? "image" : slug;
    }

    public static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        name = name.Replace('\\', '/');
        if (name.Contains('/'))
            name = name[(name.LastIndexOf('/') + 1)..];

        name = InvalidFileChars().Replace(name, "-");
        return string.IsNullOrWhiteSpace(name) ? "image.bin" : name.ToLowerInvariant();
    }

    public static string BuildPublicUrl(string publicBaseUrl, string key, bool prependUploadsSegment = false)
    {
        var baseUrl = (publicBaseUrl ?? string.Empty).TrimEnd('/');
        var normalizedKey = (key ?? string.Empty).TrimStart('/').Replace('\\', '/');

        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("PublicBaseUrl must be configured for object storage.");

        if (prependUploadsSegment)
            return $"{baseUrl}/uploads/{normalizedKey}";

        return $"{baseUrl}/{normalizedKey}";
    }

    [GeneratedRegex(@"[^a-zA-Z0-9._-]")]
    private static partial Regex InvalidFileChars();
}
