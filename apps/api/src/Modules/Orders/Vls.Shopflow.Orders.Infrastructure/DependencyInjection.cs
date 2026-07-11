using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vls.Shopflow.Orders.Application.Interfaces;
using Vls.Shopflow.Orders.Application.Options;
using Vls.Shopflow.Orders.Application.Repositories;
using Vls.Shopflow.Orders.Infrastructure.Repositories;
using Vls.Shopflow.Orders.Infrastructure.Services;
using Vls.Shopflow.Orders.Infrastructure.UnitOfWork;

namespace Vls.Shopflow.Orders.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddOrdersModule(
        this IServiceCollection services,
        string connectionString,
        IConfiguration? configuration = null,
        bool enableSensitiveLoggingOnDev = false)
    {
        services.AddDbContext<OrdersDbContext>(opt =>
        {
            opt.UseNpgsql(connectionString, npg =>
            {
                npg.MigrationsHistoryTable("__EFMigrationsHistory", "orders");
            });

            if (enableSensitiveLoggingOnDev)
                opt.EnableSensitiveDataLogging();
        });

        if (configuration is not null)
            services.Configure<GuestOrderAccessOptions>(configuration.GetSection(GuestOrderAccessOptions.SectionName));
        else
            services.Configure<GuestOrderAccessOptions>(_ => { });

        RegisterServices(services);
        return services;
    }

    public static IServiceCollection AddOrdersModuleFromConfig(
        this IServiceCollection services,
        IConfiguration configuration,
        bool enableSensitiveLoggingOnDev = false)
    {
        var cs = configuration.GetConnectionString("Orders")
                 ?? configuration.GetConnectionString("Catalog")
                 ?? throw new InvalidOperationException("ConnectionStrings:Orders or Catalog not configured.");
        return services.AddOrdersModule(cs, configuration, enableSensitiveLoggingOnDev);
    }

    private static void RegisterServices(IServiceCollection services)
    {
        services.AddScoped<IOrdersUnitOfWork, OrdersUnitOfWork>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IGuestOrderAccessTokenRepository, GuestOrderAccessTokenRepository>();
        services.AddScoped<ICheckoutSessionReader, CheckoutSessionReader>();
        services.AddScoped<IOrderPixPaymentStatusReader, NullOrderPixPaymentStatusReader>();
        services.AddSingleton<IGuestOrderAccessTokenHasher, GuestOrderAccessTokenHasher>();
    }
}
