namespace Vls.Shopflow.Notifications.Application.Options;

/// <summary>Storefront/admin base URLs used to build links in transactional emails.</summary>
public sealed class PublicAppOptions
{
    public const string SectionName = "PublicApp";

    /// <summary>e.g. https://loja.exemplo.com.br (no trailing slash). Alias: AppUrls:StorefrontBaseUrl.</summary>
    public string BaseUrl { get; set; } = "http://localhost:5173";

    /// <summary>Backoffice origin. Defaults to <see cref="BaseUrl"/>. Alias: AppUrls:AdminBaseUrl.</summary>
    public string AdminBaseUrl { get; set; } = "";

    public string StoreName { get; set; } = "Vip Assessoria";

    public string StorefrontBaseUrl => Trim(BaseUrl);

    public string ResolvedAdminBaseUrl
        => string.IsNullOrWhiteSpace(AdminBaseUrl) ? StorefrontBaseUrl : Trim(AdminBaseUrl);

    private static string Trim(string? value) => (value ?? "").Trim().TrimEnd('/');
}
