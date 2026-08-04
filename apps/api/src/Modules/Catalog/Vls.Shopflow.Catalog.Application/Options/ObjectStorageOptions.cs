namespace Vls.Shopflow.Catalog.Application.Options;

/// <summary>
/// Object storage for product images.
/// Development default: <c>Provider=Local</c> (wwwroot/uploads).
/// Testing/Production: <c>Provider=CloudflareR2</c>.
/// </summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public const string ProviderLocal = "Local";
    public const string ProviderCloudflareR2 = "CloudflareR2";

    /// <summary>Local | CloudflareR2</summary>
    public string Provider { get; set; } = ProviderLocal;

    public R2StorageOptions R2 { get; set; } = new();

    public LocalStorageOptions Local { get; set; } = new();

    public long MaxImageBytes { get; set; } = 5 * 1024 * 1024;

    public bool UseCloudflareR2 =>
        string.Equals(Provider, ProviderCloudflareR2, StringComparison.OrdinalIgnoreCase);

    public string KeyPrefix =>
        UseCloudflareR2
            ? (string.IsNullOrWhiteSpace(R2.KeyPrefix) ? "products" : R2.KeyPrefix.Trim().Trim('/'))
            : "products";
}

public sealed class R2StorageOptions
{
    /// <summary>https://&lt;ACCOUNT_ID&gt;.r2.cloudflarestorage.com</summary>
    public string Endpoint { get; set; } = "";

    public string AccountId { get; set; } = "";
    public string Bucket { get; set; } = "";
    public string AccessKeyId { get; set; } = "";
    public string SecretAccessKey { get; set; } = "";
    public string Region { get; set; } = "auto";
    public bool ForcePathStyle { get; set; } = true;

    /// <summary>Custom domain for public reads (no trailing slash).</summary>
    public string PublicBaseUrl { get; set; } = "";

    public string KeyPrefix { get; set; } = "products";

    public const string ImageCacheControl = "public, max-age=31536000, immutable";
}

public sealed class LocalStorageOptions
{
    public string RootPath { get; set; } = "";
    public string PublicBaseUrl { get; set; } = "";
}
