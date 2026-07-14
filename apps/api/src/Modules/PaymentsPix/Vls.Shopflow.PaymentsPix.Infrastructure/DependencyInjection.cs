using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vls.Shopflow.PaymentsPix.Application.Interfaces;
using Vls.Shopflow.PaymentsPix.Application.Options;
using Vls.Shopflow.PaymentsPix.Application.Repositories;
using Vls.Shopflow.PaymentsPix.Infrastructure.MercadoPago;
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
        IConfiguration? configuration = null,
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

        RegisterServices(services, configuration);
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

        services.Configure<PaymentsPixOptions>(configuration.GetSection(PaymentsPixOptions.SectionName));
        services.Configure<MercadoPagoOptions>(configuration.GetSection(MercadoPagoOptions.SectionName));

        return services.AddPaymentsPixModule(cs, configuration, enableSensitiveLoggingOnDev);
    }

    private static void RegisterServices(IServiceCollection services, IConfiguration? configuration)
    {
        services.AddScoped<IPaymentsPixUnitOfWork, PaymentsPixUnitOfWork>();
        services.AddScoped<IPixPaymentRepository, PixPaymentRepository>();
        services.AddScoped<IMercadoPagoWebhookEventRepository, MercadoPagoWebhookEventRepository>();
        services.AddScoped<IOrderPaymentReader, OrderPaymentReader>();
        services.AddScoped<IOrderPaidWriter, OrderPaidWriter>();
        services.AddScoped<ICheckoutReservationIdsReader, CheckoutReservationIdsReader>();
        services.AddScoped<IInventoryReservationConfirmer, InventoryReservationConfirmer>();
        services.AddScoped<Vls.Shopflow.Orders.Application.Interfaces.IOrderPixPaymentStatusReader, OrderPixPaymentStatusReader>();
        services.AddSingleton<IMercadoPagoOfficialWebhookSignatureClient, MercadoPagoOfficialWebhookSignatureClient>();
        services.AddSingleton<ManualMercadoPagoWebhookSignatureValidator>();
        services.AddSingleton<IMercadoPagoWebhookSignatureValidator, CompositeMercadoPagoWebhookSignatureValidator>();

        var baseUrl = configuration?["MercadoPago:BaseUrl"]?.TrimEnd('/')
                      ?? "https://api.mercadopago.com";

        services.AddHttpClient<IMercadoPagoOrderClient, MercadoPagoOrderClient>((_, client) =>
        {
            client.BaseAddress = new Uri($"{baseUrl}/");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        RegisterPixPaymentProvider(services, configuration, baseUrl);
    }

    private static void RegisterPixPaymentProvider(
        IServiceCollection services,
        IConfiguration? configuration,
        string baseUrl)
    {
        var providerName = configuration?["PaymentsPix:Provider"] ?? "Fake";

        if (string.Equals(providerName, "MercadoPago", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<IPixPaymentProvider, MercadoPagoPixPaymentProvider>((_, client) =>
            {
                client.BaseAddress = new Uri($"{baseUrl}/");
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            });

            return;
        }

        services.AddScoped<IPixPaymentProvider, FakePixPaymentProvider>();
    }
}
