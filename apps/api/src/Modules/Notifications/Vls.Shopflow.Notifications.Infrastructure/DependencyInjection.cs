using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vls.Shopflow.IdentityAccess.Application.Interfaces;
using Vls.Shopflow.Notifications.Application.Interfaces;
using Vls.Shopflow.Notifications.Application.Options;
using Vls.Shopflow.Notifications.Application.Services;
using Vls.Shopflow.Notifications.Infrastructure.Repositories;
using Vls.Shopflow.Notifications.Infrastructure.Services;
using Vls.Shopflow.Orders.Application.Interfaces;

namespace Vls.Shopflow.Notifications.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationsModule(
        this IServiceCollection services,
        string connectionString,
        IConfiguration configuration,
        bool enableSensitiveLoggingOnDev = false)
    {
        services.Configure<BrevoOptions>(configuration.GetSection(BrevoOptions.SectionName));
        services.Configure<EmailOutboxOptions>(configuration.GetSection(EmailOutboxOptions.SectionName));
        services.Configure<OrderEmailIntentDispatcherOptions>(
            configuration.GetSection(OrderEmailIntentDispatcherOptions.SectionName));
        services.Configure<PublicAppOptions>(configuration.GetSection(PublicAppOptions.SectionName));
        services.Configure<AdminNotificationsOptions>(configuration.GetSection(AdminNotificationsOptions.SectionName));
        services.PostConfigure<PublicAppOptions>(opts =>
        {
            var urls = configuration.GetSection("AppUrls");
            var storefront = urls["StorefrontBaseUrl"];
            var admin = urls["AdminBaseUrl"];
            if (!string.IsNullOrWhiteSpace(storefront))
                opts.BaseUrl = storefront.Trim();
            if (!string.IsNullOrWhiteSpace(admin))
                opts.AdminBaseUrl = admin.Trim();
            if (string.IsNullOrWhiteSpace(opts.AdminBaseUrl))
                opts.AdminBaseUrl = opts.BaseUrl;
        });

        services.AddDbContext<NotificationsDbContext>(opt =>
        {
            opt.UseNpgsql(connectionString, npg =>
            {
                npg.MigrationsHistoryTable("__EFMigrationsHistory", "notifications");
            });

            if (enableSensitiveLoggingOnDev)
                opt.EnableSensitiveDataLogging();
        });

        var brevo = configuration.GetSection(BrevoOptions.SectionName);
        var baseUrl = brevo.GetValue<string>("BaseUrl")?.TrimEnd('/') ?? "https://api.brevo.com";
        var timeoutSeconds = brevo.GetValue("TimeoutSeconds", 10);
        if (timeoutSeconds <= 0)
            timeoutSeconds = 10;

        services.AddHttpClient<ITransactionalEmailSender, BrevoTransactionalEmailSender>((_, client) =>
        {
            client.BaseAddress = new Uri($"{baseUrl}/");
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });

        services.AddScoped<IEmailOutboxRepository, EmailOutboxRepository>();
        services.AddScoped<IEmailNotificationService, EmailNotificationService>();
        services.AddScoped<IEmailOutboxProcessor, EmailOutboxProcessor>();
        services.AddScoped<IOrderEmailIntentDispatcher, OrderEmailIntentDispatcher>();

        // Override Identity logging stub + Orders null notifier when Notifications is registered last.
        services.AddScoped<IIdentityEmailSender, OutboxIdentityEmailSender>();
        services.AddScoped<IOrderEmailNotifier, OrderEmailNotifier>();
        services.AddScoped<OutboxCustomerAccessNotifier>();
        services.AddScoped<ICustomerAccessNotifier>(sp => sp.GetRequiredService<OutboxCustomerAccessNotifier>());
        services.AddScoped<ICustomerPendingApprovalNotifier>(sp =>
            sp.GetRequiredService<OutboxCustomerAccessNotifier>());

        return services;
    }

    public static IServiceCollection AddNotificationsModuleFromConfig(
        this IServiceCollection services,
        IConfiguration configuration,
        bool enableSensitiveLoggingOnDev = false)
    {
        var cs = configuration.GetConnectionString("Notifications")
                 ?? configuration.GetConnectionString("Catalog")
                 ?? throw new InvalidOperationException("ConnectionStrings:Notifications or Catalog not configured.");
        return services.AddNotificationsModule(cs, configuration, enableSensitiveLoggingOnDev);
    }
}
