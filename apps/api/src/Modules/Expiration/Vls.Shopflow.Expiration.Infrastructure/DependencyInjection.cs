using Microsoft.Extensions.DependencyInjection;
using Vls.Shopflow.Expiration.Application.Interfaces;
using Vls.Shopflow.Expiration.Infrastructure.Services;

namespace Vls.Shopflow.Expiration.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddExpirationInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IExpirationRecoveryReader, ExpirationRecoveryReader>();
        return services;
    }
}
