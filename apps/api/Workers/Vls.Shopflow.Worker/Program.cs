using Vls.Shopflow.CartCheckout.Infrastructure;
using Vls.Shopflow.Expiration.Application;
using Vls.Shopflow.Expiration.Infrastructure;
using Vls.Shopflow.Inventory.Infrastructure;
using Vls.Shopflow.Orders.Infrastructure;
using Vls.Shopflow.PaymentsPix.Infrastructure;
using Vls.Shopflow.Worker;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Catalog")
    ?? throw new InvalidOperationException("ConnectionStrings:Catalog is not configured.");

builder.Services.AddInventoryModule(connectionString);
builder.Services.AddCartCheckoutModule(connectionString);
builder.Services.AddOrdersModule(connectionString);
builder.Services.AddPaymentsPixModule(connectionString);
builder.Services.AddExpirationApplication();
builder.Services.AddExpirationInfrastructure();

builder.Services.AddHostedService<PendingCheckoutExpirationWorker>();

var host = builder.Build();
host.Run();
