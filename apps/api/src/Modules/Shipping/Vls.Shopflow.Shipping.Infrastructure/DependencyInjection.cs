using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vls.Shopflow.Shipping.Application.Interfaces;
using Vls.Shopflow.Shipping.Application.Options;
using Vls.Shopflow.Shipping.Infrastructure.ViaCep;

namespace Vls.Shopflow.Shipping.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddShippingModuleFromConfig(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<PostalCodeLookupOptions>(
            configuration.GetSection(PostalCodeLookupOptions.SectionName));

        var section = configuration.GetSection(PostalCodeLookupOptions.SectionName);
        var provider = section.GetValue<string>("Provider")?.Trim() ?? "ViaCep";
        var baseUrl = section.GetValue<string>("BaseUrl")?.TrimEnd('/')
                      ?? "https://viacep.com.br";
        var timeoutSeconds = section.GetValue("TimeoutSeconds", 5);
        if (timeoutSeconds <= 0)
            timeoutSeconds = 5;

        if (!string.Equals(provider, "ViaCep", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(provider))
        {
            // Only ViaCep is implemented; keep configurable for future providers.
            // Unknown providers still register ViaCep as default MVP implementation.
        }

        services.AddHttpClient<IPostalCodeLookupService, ViaCepPostalCodeLookupService>((_, client) =>
        {
            client.BaseAddress = new Uri($"{baseUrl}/");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
        });

        return services;
    }
}
