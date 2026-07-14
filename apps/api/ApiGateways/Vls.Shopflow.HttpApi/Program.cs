using System.Reflection;

using FluentValidation;

using MediatR;

using Microsoft.AspNetCore.HttpOverrides;

using Microsoft.AspNetCore.Identity;

using Microsoft.EntityFrameworkCore;

using Scalar.AspNetCore;

using Vls.Shopflow.Catalog.Application.Behaviors;

using Vls.Shopflow.Catalog.Infrastructure;

using Vls.Shopflow.Catalog.Infrastructure.Seed;

using Vls.Shopflow.CartCheckout.Infrastructure;

using Vls.Shopflow.IdentityAccess.Infrastructure;

using Vls.Shopflow.IdentityAccess.Infrastructure.Identity;

using Vls.Shopflow.IdentityAccess.Infrastructure.Seed;

using Vls.Shopflow.Inventory.Infrastructure;

using Vls.Shopflow.Inventory.Infrastructure.Seed;

using Vls.Shopflow.Orders.Infrastructure;

using Vls.Shopflow.PaymentsPix.Infrastructure;

using Vls.Shopflow.HttpApi.Endpoints;



var builder = WebApplication.CreateBuilder(args);



builder.WebHost.ConfigureKestrel(options =>

{

    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024;

});



builder.Services.AddOpenApi();



var appAssemblies = Directory

    .EnumerateFiles(AppContext.BaseDirectory, "Vls.Shopflow.*.Application.dll")

    .Select(Assembly.LoadFrom)

    .ToArray();



builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(appAssemblies));

builder.Services.AddValidatorsFromAssemblies(appAssemblies);



builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));



builder.Services.AddCatalogModuleFromConfig(builder.Configuration, enableSensitiveLoggingOnDev: builder.Environment.IsDevelopment());

builder.Services.AddInventoryModuleFromConfig(builder.Configuration, enableSensitiveLoggingOnDev: builder.Environment.IsDevelopment());

builder.Services.AddCartCheckoutModuleFromConfig(builder.Configuration, enableSensitiveLoggingOnDev: builder.Environment.IsDevelopment());

builder.Services.AddOrdersModuleFromConfig(builder.Configuration, enableSensitiveLoggingOnDev: builder.Environment.IsDevelopment());

builder.Services.AddPaymentsPixModuleFromConfig(builder.Configuration, enableSensitiveLoggingOnDev: builder.Environment.IsDevelopment());

builder.Services.AddIdentityAccessModuleFromConfig(builder.Configuration, builder.Environment, enableSensitiveLoggingOnDev: builder.Environment.IsDevelopment());

// API runs HTTP inside Docker behind Caddy/Cloudflare TLS termination.
// Forwarded headers restore Request.Scheme=https so Secure cookies (antiforgery) work.
// Only trust X-Forwarded-* from private Docker/loopback peers (Caddy), never from any client.
builder.Services.Configure<ForwardedHeadersOptions>(Vls.Shopflow.HttpApi.ForwardedHeadersConfiguration.Configure);

var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
    ?.Where(static o => !string.IsNullOrWhiteSpace(o))
    .ToArray() ?? [];

if (allowedOrigins.Length == 0)
{
    allowedOrigins =
    [
        "http://localhost:8080",
        "http://localhost:5173",
        "http://localhost:3000"
    ];
}

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        Vls.Shopflow.IdentityAccess.Infrastructure.DependencyInjection.CorsPolicyName,
        policy => policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

builder.Services.AddEndpointsApiExplorer();



var app = builder.Build();

{
    var paymentsPixLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("PaymentsPix.Startup");
    var provider = builder.Configuration["PaymentsPix:Provider"] ?? "Fake";
    var mpEnvironment = builder.Configuration["MercadoPago:Environment"] ?? "(unset)";
    var accessTokenConfigured = !string.IsNullOrWhiteSpace(builder.Configuration["MercadoPago:AccessToken"]);
    var webhookSecretConfigured = !string.IsNullOrWhiteSpace(builder.Configuration["MercadoPago:WebhookSecret"]);
    var notificationUrlConfigured = !string.IsNullOrWhiteSpace(builder.Configuration["MercadoPago:NotificationUrl"]);
    paymentsPixLogger.LogInformation(
        "PaymentsPix provider: {Provider}. MercadoPago environment: {Environment}. " +
        "MercadoPago access token configured: {AccessTokenConfigured}. " +
        "MercadoPago webhook secret configured: {WebhookSecretConfigured}. " +
        "MercadoPago notification URL configured: {NotificationUrlConfigured}.",
        provider,
        mpEnvironment,
        accessTokenConfigured,
        webhookSecretConfigured,
        notificationUrlConfigured);
}



var uploadsRoot = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "uploads");

Directory.CreateDirectory(uploadsRoot);

Directory.CreateDirectory(Path.Combine(uploadsRoot, "products"));

Directory.CreateDirectory(Path.Combine(uploadsRoot, "seed-products"));



using (var scope = app.Services.CreateScope())

{

    var catalogDb = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

    await catalogDb.Database.MigrateAsync();

    await CatalogDbContextSeed.SeedAsync(catalogDb);



    var inventoryDb = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

    await inventoryDb.Database.MigrateAsync();



    var demoSeedLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
        .CreateLogger("DemoClothingCatalogSeed");

    var demoOptions = builder.Configuration.GetSection(DemoCatalogSeedOptions.SectionName)
        .Get<DemoCatalogSeedOptions>() ?? new DemoCatalogSeedOptions();

    await DemoClothingCatalogSeed.SeedAsync(
        catalogDb,
        builder.Configuration,
        app.Environment,
        app.Environment,
        demoSeedLogger);

    if (demoOptions.Enabled && demoOptions.CreateInventory)
    {
        var demoSkuIds = await DemoClothingCatalogSeed.GetDemoSkuIdsAsync(catalogDb);
        var inventorySeedLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("DemoClothingInventorySeed");

        await DemoClothingInventorySeed.SeedAsync(
            inventoryDb,
            demoSkuIds,
            demoOptions.DefaultStockQuantity,
            DemoClothingCatalogSeed.InventoryReason,
            inventorySeedLogger);
    }



    var cartCheckoutDb = scope.ServiceProvider.GetRequiredService<CartCheckoutDbContext>();

    await cartCheckoutDb.Database.MigrateAsync();



    var ordersDb = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();

    await ordersDb.Database.MigrateAsync();



    var paymentsPixDb = scope.ServiceProvider.GetRequiredService<PaymentsPixDbContext>();

    await paymentsPixDb.Database.MigrateAsync();



    var identityDb = scope.ServiceProvider.GetRequiredService<IdentityAccessDbContext>();

    await identityDb.Database.MigrateAsync();



    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ShopflowUser>>();

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ShopflowRole>>();

    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("IdentityAccessSeed");

    await IdentityAccessDbContextSeed.SeedAsync(userManager, roleManager, builder.Configuration, app.Environment, logger);

}



if (app.Environment.IsDevelopment())

{

    app.MapOpenApi();

    app.MapScalarApiReference(options =>

    {

        options.Title = "Shopflow API";

        options.OpenApiRoutePattern = "/openapi/{documentName}.json";

    });

}



// Must run before exception handling, CORS, auth, cookies, CSRF and endpoints
// that depend on Request.Scheme (Secure antiforgery cookies behind TLS proxy).
app.UseForwardedHeaders();

app.Use(async (ctx, next) =>

{

    try

    {

        await next();

    }

    catch (ValidationException ex)

    {

        ctx.Response.StatusCode = StatusCodes.Status400BadRequest;

        await ctx.Response.WriteAsJsonAsync(new {

            message = "Validation failed",

            errors  = ex.Errors.Select(e => new { e.PropertyName, e.ErrorMessage })

        });

    }

    catch (KeyNotFoundException ex)

    {

        ctx.Response.StatusCode = StatusCodes.Status404NotFound;

        await ctx.Response.WriteAsJsonAsync(new { message = ex.Message });

    }

    catch (Vls.Shopflow.Inventory.Domain.Exceptions.SkuNotFoundException ex)

    {

        ctx.Response.StatusCode = StatusCodes.Status404NotFound;

        await ctx.Response.WriteAsJsonAsync(new { message = ex.Message, skuId = ex.SkuId });

    }

    catch (Vls.Shopflow.Inventory.Domain.Exceptions.InventoryItemNotFoundException ex)

    {

        ctx.Response.StatusCode = StatusCodes.Status404NotFound;

        await ctx.Response.WriteAsJsonAsync(new { message = ex.Message, skuId = ex.SkuId });

    }

    catch (Vls.Shopflow.Inventory.Domain.Exceptions.StockReservationNotFoundException ex)

    {

        ctx.Response.StatusCode = StatusCodes.Status404NotFound;

        await ctx.Response.WriteAsJsonAsync(new { message = ex.Message, reservationId = ex.ReservationId });

    }

    catch (Vls.Shopflow.Inventory.Domain.Exceptions.InvalidStockQuantityException ex)

    {

        ctx.Response.StatusCode = StatusCodes.Status400BadRequest;

        await ctx.Response.WriteAsJsonAsync(new { message = ex.Message });

    }

    catch (Vls.Shopflow.Inventory.Domain.Exceptions.InventoryItemAlreadyExistsException ex)

    {

        ctx.Response.StatusCode = StatusCodes.Status409Conflict;

        await ctx.Response.WriteAsJsonAsync(new { message = ex.Message, skuId = ex.SkuId });

    }

    catch (Vls.Shopflow.Inventory.Domain.Exceptions.InsufficientStockException ex)

    {

        ctx.Response.StatusCode = StatusCodes.Status409Conflict;

        await ctx.Response.WriteAsJsonAsync(new

        {

            message = ex.Message,

            skuId = ex.SkuId,

            requested = ex.Requested,

            available = ex.Available

        });

    }

    catch (Vls.Shopflow.Inventory.Domain.Exceptions.InvalidStockReservationStatusException ex)

    {

        ctx.Response.StatusCode = StatusCodes.Status409Conflict;

        await ctx.Response.WriteAsJsonAsync(new { message = ex.Message, reservationId = ex.ReservationId });

    }

    catch (Vls.Shopflow.CartCheckout.Domain.Exceptions.CheckoutSessionNotFoundException ex)

    {

        ctx.Response.StatusCode = StatusCodes.Status404NotFound;

        await ctx.Response.WriteAsJsonAsync(new { message = ex.Message, checkoutSessionId = ex.CheckoutSessionId });

    }

    catch (Vls.Shopflow.CartCheckout.Domain.Exceptions.InvalidCheckoutSessionStatusException ex)

    {

        ctx.Response.StatusCode = StatusCodes.Status409Conflict;

        await ctx.Response.WriteAsJsonAsync(new { message = ex.Message, checkoutSessionId = ex.CheckoutSessionId });

    }

    catch (Vls.Shopflow.CartCheckout.Domain.Exceptions.CatalogSkuNotFoundException ex)

    {

        ctx.Response.StatusCode = StatusCodes.Status404NotFound;

        await ctx.Response.WriteAsJsonAsync(new { message = ex.Message, skuId = ex.SkuId });

    }

    catch (Vls.Shopflow.CartCheckout.Domain.Exceptions.InactiveSkuException ex)

    {

        ctx.Response.StatusCode = StatusCodes.Status409Conflict;

        await ctx.Response.WriteAsJsonAsync(new { message = ex.Message, skuId = ex.SkuId });

    }

    catch (Vls.Shopflow.Orders.Domain.Exceptions.OrderNotFoundException ex)

    {

        ctx.Response.StatusCode = StatusCodes.Status404NotFound;

        await ctx.Response.WriteAsJsonAsync(new { message = ex.Message, orderId = ex.OrderId });

    }

    catch (Vls.Shopflow.Orders.Domain.Exceptions.OrderNotFoundByCheckoutSessionException ex)

    {

        ctx.Response.StatusCode = StatusCodes.Status404NotFound;

        await ctx.Response.WriteAsJsonAsync(new { message = ex.Message, checkoutSessionId = ex.CheckoutSessionId });

    }

    catch (Vls.Shopflow.Orders.Domain.Exceptions.CheckoutSessionNotFoundForOrderException ex)

    {

        ctx.Response.StatusCode = StatusCodes.Status404NotFound;

        await ctx.Response.WriteAsJsonAsync(new { message = ex.Message, checkoutSessionId = ex.CheckoutSessionId });

    }

    catch (Vls.Shopflow.Orders.Domain.Exceptions.InvalidCheckoutSessionForOrderException ex)

    {

        ctx.Response.StatusCode = StatusCodes.Status409Conflict;

        await ctx.Response.WriteAsJsonAsync(new { message = ex.Message, checkoutSessionId = ex.CheckoutSessionId });

    }

    catch (Vls.Shopflow.Orders.Domain.Exceptions.OrderAlreadyExistsForCheckoutSessionException ex)

    {

        ctx.Response.StatusCode = StatusCodes.Status409Conflict;

        await ctx.Response.WriteAsJsonAsync(new

        {

            message = ex.Message,

            checkoutSessionId = ex.CheckoutSessionId,

            existingOrderId = ex.ExistingOrderId

        });

    }

    catch (Vls.Shopflow.Orders.Domain.Exceptions.GuestOrderAccessDeniedException)

    {

        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;

        await ctx.Response.WriteAsJsonAsync(new { message = "Order access denied." });

    }

    catch (Vls.Shopflow.Orders.Domain.Exceptions.GuestOrderAccessMisconfiguredException ex)

    {

        ctx.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;

        await ctx.Response.WriteAsJsonAsync(new { message = ex.Message });

    }

    catch (Vls.Shopflow.PaymentsPix.Domain.Exceptions.PixPaymentNotFoundException ex)

    {

        ctx.Response.StatusCode = StatusCodes.Status404NotFound;

        await ctx.Response.WriteAsJsonAsync(new { message = ex.Message, paymentId = ex.PaymentId });

    }

    catch (Vls.Shopflow.PaymentsPix.Domain.Exceptions.PixPaymentNotFoundForOrderException ex)

    {

        ctx.Response.StatusCode = StatusCodes.Status404NotFound;

        await ctx.Response.WriteAsJsonAsync(new { message = ex.Message, orderId = ex.OrderId });

    }

    catch (Vls.Shopflow.PaymentsPix.Domain.Exceptions.OrderNotFoundForPixPaymentException ex)

    {

        ctx.Response.StatusCode = StatusCodes.Status404NotFound;

        await ctx.Response.WriteAsJsonAsync(new { message = ex.Message, orderId = ex.OrderId });

    }

    catch (Vls.Shopflow.PaymentsPix.Domain.Exceptions.OrderNotEligibleForPixPaymentException ex)

    {

        ctx.Response.StatusCode = StatusCodes.Status409Conflict;

        await ctx.Response.WriteAsJsonAsync(new

        {

            message = ex.Message,

            orderId = ex.OrderId,

            orderStatus = ex.OrderStatus

        });

    }

    catch (Vls.Shopflow.PaymentsPix.Domain.Exceptions.InvalidOrderTotalForPixPaymentException ex)

    {

        ctx.Response.StatusCode = StatusCodes.Status400BadRequest;

        await ctx.Response.WriteAsJsonAsync(new

        {

            message = ex.Message,

            orderId = ex.OrderId,

            total = ex.Total

        });

    }

    catch (Vls.Shopflow.PaymentsPix.Domain.Exceptions.MercadoPagoPixChargeFailedException ex)

    {

        ctx.Response.StatusCode = StatusCodes.Status502BadGateway;

        await ctx.Response.WriteAsJsonAsync(new

        {

            message = ex.Message,

            orderId = ex.OrderId,

            providerStatusCode = ex.StatusCode,

            providerMessage = ex.ProviderMessage

        });

    }

});



app.UseCors(Vls.Shopflow.IdentityAccess.Infrastructure.DependencyInjection.CorsPolicyName);

app.UseStaticFiles();

app.UseRateLimiter();

app.UseIdentityAccessMiddleware();



app.MapHealthEndpoints();

app.MapGroup("/api").MapAdminAuthEndpoints();

app.MapGroup("/api").MapCustomerAuthEndpoints();

app.MapGroup("/api").MapCatalogEndpoints();

app.MapGroup("/api").MapInventoryEndpoints();

app.MapGroup("/api").MapCheckoutEndpoints();

app.MapGroup("/api").MapOrdersEndpoints();

app.MapGroup("/api").MapPaymentsPixEndpoints();



app.Run();



public partial class Program;


