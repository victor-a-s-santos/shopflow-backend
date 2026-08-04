using Vls.Shopflow.Catalog.Application.Options;
using Vls.Shopflow.Catalog.Application.Services;

namespace Vls.Shopflow.Catalog.Application.Services.ProductImageR2Backfill;

public static class ProductImageBackfillGuards
{
    public static void EnsureSafeToRun(ProductImageBackfillOptions options)
    {
        if (IsProduction(options.EnvironmentName))
            throw new InvalidOperationException(
                "Product image R2 backfill is forbidden in Production.");

        if (LooksLikeProductionConnectionString(options.ConnectionString))
            throw new InvalidOperationException(
                "Connection string appears to target a production database. Aborting backfill.");

        if (!Directory.Exists(options.SourceRoot))
            throw new InvalidOperationException(
                $"Source root does not exist: {options.SourceRoot}");

        if (!string.Equals(options.StorageProvider, StorageOptions.ProviderCloudflareR2, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Storage:Provider must be CloudflareR2 for backfill (configure TEST env).");

        if (string.IsNullOrWhiteSpace(options.R2Bucket))
            throw new InvalidOperationException("Storage:R2:Bucket is required.");

        if (string.IsNullOrWhiteSpace(options.R2PublicBaseUrl))
            throw new InvalidOperationException("Storage:R2:PublicBaseUrl is required.");

        if (!options.Execute)
            return;

        if (!options.BackfillFlagEnabled)
            throw new InvalidOperationException(
                "R2ImageBackfill:Enabled must be true to execute. Dry-run does not require it.");

        if (!string.Equals(options.ConfirmPhrase, R2ImageBackfillOptions.ConfirmPhrase, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Execute requires --confirm {R2ImageBackfillOptions.ConfirmPhrase}");
    }

    public static bool IsProduction(string? environmentName)
    {
        if (string.IsNullOrWhiteSpace(environmentName))
            return false;

        return string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase)
               || string.Equals(environmentName, "Prod", StringComparison.OrdinalIgnoreCase);
    }

    public static bool LooksLikeProductionConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return false;

        var cs = connectionString.ToLowerInvariant();
        // Explicit prod markers — avoid matching "shopflow" alone.
        string[] markers =
        [
            "database=shopflow_prod",
            "database=shopflow-prod",
            "database=shopflowprod",
            "database=prod_shopflow",
            "host=prod.",
            "host=db-prod",
            ".prod.",
            "production"
        ];

        return markers.Any(m => cs.Contains(m, StringComparison.Ordinal));
    }
}

public static class ProductImageBackfillSelector
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp"
    };

    public static bool IsAlreadyOnR2(string? storageProvider)
        => string.Equals(storageProvider, StorageOptions.ProviderCloudflareR2, StringComparison.OrdinalIgnoreCase);

    public static bool IsEligibleProvider(string? storageProvider)
        => string.IsNullOrWhiteSpace(storageProvider)
           || string.Equals(storageProvider, StorageOptions.ProviderLocal, StringComparison.OrdinalIgnoreCase);

    public static string? TryResolveLocalRelativePath(string? objectKey, string? url)
    {
        if (!string.IsNullOrWhiteSpace(objectKey))
            return objectKey.Replace('\\', '/').TrimStart('/');

        url ??= string.Empty;
        var marker = "/uploads/";
        var idx = url.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
            return url[(idx + marker.Length)..].TrimStart('/');

        return null;
    }

    public static string GuessContentType(string pathOrFile)
        => ProductImageStorageKeys.NormalizeExtension(Path.GetExtension(pathOrFile)) switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".jpg" => "image/jpeg",
            _ => "application/octet-stream"
        };

    public static string BuildPlannedObjectKey(
        string keyPrefix,
        Guid productId,
        Guid imageId,
        string? productSlug,
        string localRelativePath)
    {
        var ext = ProductImageStorageKeys.NormalizeExtension(Path.GetExtension(localRelativePath));
        if (ext == ".bin")
            ext = ".jpg";

        // Preserve seed layout when migrating seed-products/*
        if (localRelativePath.StartsWith("seed-products/", StringComparison.OrdinalIgnoreCase)
            || localRelativePath.Contains("/seed/", StringComparison.OrdinalIgnoreCase))
        {
            var fileName = Path.GetFileName(localRelativePath);
            var slug = string.IsNullOrWhiteSpace(productSlug) ? "seed" : productSlug;
            return ProductImageStorageKeys.BuildSeedKey(keyPrefix, slug, fileName);
        }

        return ProductImageStorageKeys.Build(keyPrefix, productId, imageId, productSlug, ext);
    }

    public static bool IsAllowedExtension(string path)
    {
        var ext = Path.GetExtension(path);
        return AllowedExtensions.Contains(ext);
    }
}
