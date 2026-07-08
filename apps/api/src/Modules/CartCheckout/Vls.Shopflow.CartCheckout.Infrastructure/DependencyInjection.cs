using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vls.Shopflow.CartCheckout.Application.Interfaces;
using Vls.Shopflow.CartCheckout.Application.Repositories;
using Vls.Shopflow.CartCheckout.Infrastructure.Repositories;
using Vls.Shopflow.CartCheckout.Infrastructure.Services;
using Vls.Shopflow.CartCheckout.Infrastructure.UnitOfWork;

namespace Vls.Shopflow.CartCheckout.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCartCheckoutModule(
        this IServiceCollection services,
        string connectionString,
        bool enableSensitiveLoggingOnDev = false)
    {
        services.AddDbContext<CartCheckoutDbContext>(opt =>
        {
            opt.UseNpgsql(connectionString, npg =>
            {
                npg.MigrationsHistoryTable("__EFMigrationsHistory", "cartcheckout");
            });

            if (enableSensitiveLoggingOnDev)
                opt.EnableSensitiveDataLogging();
        });

        RegisterServices(services);
        return services;
    }

    public static IServiceCollection AddCartCheckoutModule(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> dbOptionsBuilder)
    {
        services.AddDbContext<CartCheckoutDbContext>(dbOptionsBuilder);
        RegisterServices(services);
        return services;
    }

    public static IServiceCollection AddCartCheckoutModuleFromConfig(
        this IServiceCollection services,
        IConfiguration configuration,
        bool enableSensitiveLoggingOnDev = false)
    {
        var cs = configuration.GetConnectionString("CartCheckout")
                 ?? configuration.GetConnectionString("Catalog")
                 ?? throw new InvalidOperationException("ConnectionStrings:CartCheckout or Catalog not configured.");
        return services.AddCartCheckoutModule(cs, enableSensitiveLoggingOnDev);
    }

    private static void RegisterServices(IServiceCollection services)
    {
        services.AddScoped<ICartCheckoutUnitOfWork, CartCheckoutUnitOfWork>();
        services.AddScoped<ICheckoutSessionRepository, CheckoutSessionRepository>();
        services.AddScoped<ICatalogSkuPricingService, CatalogSkuPricingService>();
        services.AddScoped<IInventoryReservationService, InventoryReservationService>();
    }
}
