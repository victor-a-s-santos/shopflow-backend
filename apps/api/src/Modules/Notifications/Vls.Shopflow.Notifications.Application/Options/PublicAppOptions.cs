namespace Vls.Shopflow.Notifications.Application.Options;

/// <summary>Storefront base URL used to build links in transactional emails.</summary>
public sealed class PublicAppOptions
{
    public const string SectionName = "PublicApp";

    /// <summary>e.g. https://loja.exemplo.com.br (no trailing slash).</summary>
    public string BaseUrl { get; set; } = "http://localhost:5173";

    public string StoreName { get; set; } = "Vip Assessoria";
}
