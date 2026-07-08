using Microsoft.Extensions.DependencyInjection;
using Vls.Shopflow.Expiration.Application.Interfaces;
using Vls.Shopflow.Expiration.Application.Options;

namespace Vls.Shopflow.Expiration.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddExpirationApplication(this IServiceCollection services)
    {
        services.AddOptions<ExpirationWorkerOptions>()
            .BindConfiguration(ExpirationWorkerOptions.SectionName);

        services.AddScoped<IExpirationProcessor, ExpirationProcessor>();
        return services;
    }
}
