using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vls.Shopflow.PaymentsPix.Application.Interfaces;
using Vls.Shopflow.PaymentsPix.Application.Repositories;
using Vls.Shopflow.PaymentsPix.Infrastructure.Providers;
using Vls.Shopflow.PaymentsPix.Infrastructure.Repositories;
using Vls.Shopflow.PaymentsPix.Infrastructure.Services;
using Vls.Shopflow.PaymentsPix.Infrastructure.UnitOfWork;

namespace Vls.Shopflow.PaymentsPix.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPaymentsPixModule(
        this IServiceCollection services,
        string connectionString,
        bool enableSensitiveLoggingOnDev = false)
    {
        services.AddDbContext<PaymentsPixDbContext>(opt =>
        {
            opt.UseNpgsql(connectionString, npg =>
            {
                npg.MigrationsHistoryTable("__EFMigrationsHistory", "payments_pix");
            });

            if (enableSensitiveLoggingOnDev)
                opt.EnableSensitiveDataLogging();
        });

        RegisterServices(services);
        return services;
    }

    public static IServiceCollection AddPaymentsPixModuleFromConfig(
        this IServiceCollection services,
        IConfiguration configuration,
        bool enableSensitiveLoggingOnDev = false)
    {
        var cs = configuration.GetConnectionString("PaymentsPix")
                 ?? configuration.GetConnectionString("Orders")
                 ?? configuration.GetConnectionString("Catalog")
                 ?? throw new InvalidOperationException(
                     "ConnectionStrings:PaymentsPix, Orders or Catalog not configured.");

        return services.AddPaymentsPixModule(cs, enableSensitiveLoggingOnDev);
    }

    private static void RegisterServices(IServiceCollection services)
    {
        services.AddScoped<IPaymentsPixUnitOfWork, PaymentsPixUnitOfWork>();
        services.AddScoped<IPixPaymentRepository, PixPaymentRepository>();
        services.AddScoped<IOrderPaymentReader, OrderPaymentReader>();
        services.AddScoped<IPixPaymentProvider, FakePixPaymentProvider>();
    }
}
