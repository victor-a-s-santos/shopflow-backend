using Vls.Shopflow.Catalog.Application.Options;
using Vls.Shopflow.Catalog.Domain.Entities;

namespace Vls.Shopflow.Catalog.Application.Services;

/// <summary>
/// Rules for when demo-seed / R2 migration may skip vs must upload.
/// Never rewrite Url to R2 without a confirmed object.
/// </summary>
public static class DemoSeedR2MigrationRules
{
    public static bool IsCloudflareR2(string? storageProvider)
        => string.Equals(
            storageProvider,
            StorageOptions.ProviderCloudflareR2,
            StringComparison.OrdinalIgnoreCase);

    public static bool IsLegacySeedObjectKey(string? objectKey)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
            return false;

        var key = objectKey.Replace('\\', '/').TrimStart('/');
        return key.StartsWith("seed-products/", StringComparison.OrdinalIgnoreCase);
    }

    public static bool UrlMatchesR2PublicBase(string? url, string? r2PublicBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(r2PublicBaseUrl))
            return false;

        var baseUrl = r2PublicBaseUrl.TrimEnd('/');
        return url.StartsWith(baseUrl + "/", StringComparison.OrdinalIgnoreCase)
               || string.Equals(url, baseUrl, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// True when the DB row is not a complete CloudflareR2 placement for <paramref name="targetObjectKey"/>.
    /// Caller must still verify object existence via ExistsAsync when this returns false.
    /// </summary>
    public static bool NeedsR2Upload(
        string? storageProvider,
        string? objectKey,
        string? url,
        string targetObjectKey,
        string r2PublicBaseUrl)
    {
        if (!IsCloudflareR2(storageProvider))
            return true;

        if (string.IsNullOrWhiteSpace(objectKey))
            return true;

        if (IsLegacySeedObjectKey(objectKey))
            return true;

        if (!string.Equals(
                objectKey.Replace('\\', '/').TrimStart('/'),
                targetObjectKey.Replace('\\', '/').TrimStart('/'),
                StringComparison.OrdinalIgnoreCase))
            return true;

        if (!UrlMatchesR2PublicBase(url, r2PublicBaseUrl))
            return true;

        return false;
    }

    public static bool NeedsR2Upload(ProductImage image, string targetObjectKey, string r2PublicBaseUrl)
        => NeedsR2Upload(
            image.StorageProvider,
            image.ObjectKey,
            image.Url,
            targetObjectKey,
            r2PublicBaseUrl);
}
