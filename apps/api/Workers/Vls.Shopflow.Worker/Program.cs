using Vls.Shopflow.CartCheckout.Infrastructure;
using Vls.Shopflow.Expiration.Application;
using Vls.Shopflow.Expiration.Infrastructure;
using Vls.Shopflow.Inventory.Infrastructure;
using Vls.Shopflow.Notifications.Application.Options;
using Vls.Shopflow.Notifications.Infrastructure;
using Vls.Shopflow.Orders.Infrastructure;
using Vls.Shopflow.PaymentsPix.Application.Options;
using Vls.Shopflow.PaymentsPix.Infrastructure;
using Vls.Shopflow.Worker;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Catalog")
    ?? throw new InvalidOperationException("ConnectionStrings:Catalog is not configured.");

builder.Services.Configure<PaymentsPixOptions>(
    builder.Configuration.GetSection(PaymentsPixOptions.SectionName));
builder.Services.Configure<MercadoPagoOptions>(
    builder.Configuration.GetSection(MercadoPagoOptions.SectionName));
builder.Services.Configure<MercadoPagoReconciliationOptions>(
    builder.Configuration.GetSection(MercadoPagoReconciliationOptions.SectionName));
builder.Services.Configure<EmailOutboxOptions>(
    builder.Configuration.GetSection(EmailOutboxOptions.SectionName));

builder.Services.AddInventoryModule(connectionString);
builder.Services.AddCartCheckoutModule(connectionString);
builder.Services.AddOrdersModule(connectionString);
builder.Services.AddPaymentsPixModule(connectionString, builder.Configuration);
builder.Services.AddNotificationsModule(connectionString, builder.Configuration);
builder.Services.AddExpirationApplication();
builder.Services.AddExpirationInfrastructure();

builder.Services.AddHostedService<PendingCheckoutExpirationWorker>();
builder.Services.AddHostedService<MercadoPagoPixReconciliationWorker>();
builder.Services.AddHostedService<EmailOutboxWorker>();

var host = builder.Build();
host.Run();
