namespace Vls.Shopflow.Shipping.Application.Options;

public sealed class PostalCodeLookupOptions
{
    public const string SectionName = "PostalCodeLookup";

    public bool Enabled { get; set; } = true;

    /// <summary>Infrastructure provider key. Initial: ViaCep.</summary>
    public string Provider { get; set; } = "ViaCep";

    public string BaseUrl { get; set; } = "https://viacep.com.br";

    public int TimeoutSeconds { get; set; } = 5;

    public int RateLimitPerMinute { get; set; } = 60;
}
