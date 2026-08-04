using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Vls.Shopflow.CartCheckout.Domain.Entities;
using Vls.Shopflow.CartCheckout.Infrastructure;
using Vls.Shopflow.CartCheckout.Infrastructure.Repositories;
using Vls.Shopflow.CartCheckout.Infrastructure.Services;
using Vls.Shopflow.CartCheckout.Infrastructure.UnitOfWork;
using Vls.Shopflow.Expiration.Application;
using Vls.Shopflow.Expiration.Application.Options;
using Vls.Shopflow.Expiration.Infrastructure;
using Vls.Shopflow.Expiration.Infrastructure.Services;
using Vls.Shopflow.Inventory.Domain.Entities;
using Vls.Shopflow.Inventory.Domain.Enums;
using Vls.Shopflow.Inventory.Infrastructure;
using Vls.Shopflow.Inventory.Infrastructure.Repositories;
using Vls.Shopflow.Orders.Application.CommandHandlers;
using Vls.Shopflow.Orders.Application.Commands;
using Vls.Shopflow.Orders.Application.Options;
using Vls.Shopflow.Orders.Domain.Enums;
using Vls.Shopflow.Orders.Infrastructure;
using Vls.Shopflow.Orders.Infrastructure.Repositories;
using Vls.Shopflow.Orders.Infrastructure.Services;
using Vls.Shopflow.Orders.Infrastructure.UnitOfWork;
using Vls.Shopflow.PaymentsPix.Application.CommandHandlers;
using Vls.Shopflow.PaymentsPix.Application.Commands;
using Vls.Shopflow.PaymentsPix.Application.Options;
using Vls.Shopflow.PaymentsPix.Domain.Enums;
using Vls.Shopflow.PaymentsPix.Infrastructure;
using Vls.Shopflow.PaymentsPix.Infrastructure.Providers;
using Vls.Shopflow.PaymentsPix.Infrastructure.Repositories;
using Vls.Shopflow.PaymentsPix.Infrastructure.Services;
using Vls.Shopflow.PaymentsPix.Infrastructure.UnitOfWork;

namespace Vls.Shopflow.Expiration.IntegrationTests;

public sealed class ExpirationProcessorIntegrationTests
{
    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("SHOPFLOW_TEST_DB")
        ?? "Host=localhost;Port=5432;Database=shopflow;Username=postgres;Password=postgres";

    private static async Task<bool> CanConnectAsync()
    {
        try
        {
            await using var db = CreateCartCheckoutContext();
            return await db.Database.CanConnectAsync();
        }
        catch
        {
            return false;
        }
    }

    private static CartCheckoutDbContext CreateCartCheckoutContext()
    {
        var options = new DbContextOptionsBuilder<CartCheckoutDbContext>()
            .UseNpgsql(ConnectionString, npg =>
                npg.MigrationsHistoryTable("__EFMigrationsHistory", "cartcheckout"))
            .Options;
        return new CartCheckoutDbContext(options);
    }

    private static OrdersDbContext CreateOrdersContext()
    {
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseNpgsql(ConnectionString, npg =>
                npg.MigrationsHistoryTable("__EFMigrationsHistory", "orders"))
            .Options;
        return new OrdersDbContext(options);
    }

    private static PaymentsPixDbContext CreatePaymentsPixContext()
    {
        var options = new DbContextOptionsBuilder<PaymentsPixDbContext>()
            .UseNpgsql(ConnectionString, npg =>
                npg.MigrationsHistoryTable("__EFMigrationsHistory", "payments_pix"))
            .Options;
        return new PaymentsPixDbContext(options);
    }

    private static InventoryDbContext CreateInventoryContext()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseNpgsql(ConnectionString, npg =>
                npg.MigrationsHistoryTable("__EFMigrationsHistory", "inventory"))
            .Options;
        return new InventoryDbContext(options);
    }

    private static ExpirationProcessor CreateProcessor(
        CartCheckoutDbContext cartDb,
        OrdersDbContext ordersDb,
        PaymentsPixDbContext paymentsDb,
        InventoryDbContext inventoryDb)
    {
        var inventoryOps = new InventoryAtomicOperations(inventoryDb);
        var inventoryReservation = new InventoryReservationService(inventoryOps);

        return new ExpirationProcessor(
            new CheckoutSessionRepository(cartDb),
            new OrderRepository(ordersDb),
            new PixPaymentRepository(paymentsDb),
            inventoryReservation,
            new CartCheckoutUnitOfWork(cartDb),
            new OrdersUnitOfWork(ordersDb),
            new PaymentsPixUnitOfWork(paymentsDb),
            new ExpirationRecoveryReader(ordersDb, cartDb),
            Options.Create(new ExpirationWorkerOptions { BatchSize = 50, PixPaymentTtlMinutes = 15 }),
            NullLogger<ExpirationProcessor>.Instance);
    }

    [Fact]
    public async Task ProcessAsync_ExpiresFullCheckoutFlowAndReleasesStock()
    {
        if (!await CanConnectAsync())
            return;

        await using var cartDb = CreateCartCheckoutContext();
        await using var ordersDb = CreateOrdersContext();
        await using var paymentsDb = CreatePaymentsPixContext();
        await using var inventoryDb = CreateInventoryContext();

        await cartDb.Database.MigrateAsync();
        await ordersDb.Database.MigrateAsync();
        await paymentsDb.Database.MigrateAsync();
        await inventoryDb.Database.MigrateAsync();

        var skuId = Guid.NewGuid();
        inventoryDb.InventoryItems.Add(InventoryItem.Create(skuId, 10, isInitialStock: true));
        await inventoryDb.SaveChangesAsync();

        var inventoryOps = new InventoryAtomicOperations(inventoryDb);
        var reservationId = await inventoryOps.ReserveAsync(
            skuId,
            2,
            DateTimeOffset.UtcNow.AddMinutes(-1),
            CancellationToken.None);

        var item = CheckoutSessionItem.Create(
            Guid.NewGuid(),
            "Produto Expiração",
            "produto-expiracao",
            skuId,
            "SKU-EXP",
            2,
            49.90m,
            reservationId);

        var session = CheckoutSession.CreatePending(
            "Cliente Expiração",
            "expiracao@shopflow.test",
            "11999990000",
            "01001000",
            "Rua Teste",
            "100",
            null,
            "Centro",
            "São Paulo",
            "SP",
            DateTimeOffset.UtcNow.AddMinutes(-1),
            new[] { item });

        cartDb.CheckoutSessions.Add(session);
        await cartDb.SaveChangesAsync();

        var createOrderHandler = new CreateOrderFromCheckoutSessionCommandHandler(
            new CheckoutSessionReader(cartDb),
            new OrderRepository(ordersDb),
            new Vls.Shopflow.Orders.Infrastructure.Services.PostgresOrderNumberGenerator(ordersDb),
            new GuestOrderAccessTokenRepository(ordersDb),
            new GuestOrderAccessTokenHasher(Options.Create(new GuestOrderAccessOptions
            {
                Enabled = true,
                TokenTtlDays = 30,
                TokenHashSecret = "test-secret"
            })),
            new OrdersUnitOfWork(ordersDb),
            Options.Create(new GuestOrderAccessOptions
            {
                Enabled = true,
                TokenTtlDays = 30,
                TokenHashSecret = "test-secret"
            }),
            new Vls.Shopflow.Orders.Infrastructure.Services.NullOrderEmailNotifier(),
            NullLogger<CreateOrderFromCheckoutSessionCommandHandler>.Instance);

        var order = await createOrderHandler.Handle(
            new CreateOrderFromCheckoutSessionCommand(session.Id),
            CancellationToken.None);

        var createPixHandler = new CreatePixPaymentForOrderCommandHandler(
            new OrderPaymentReader(ordersDb),
            new PixPaymentRepository(paymentsDb),
            new FakePixPaymentProvider(),
            new PaymentsPixUnitOfWork(paymentsDb),
            Options.Create(new MercadoPagoOptions { PixExpirationMinutes = 30 }),
            NullLogger<CreatePixPaymentForOrderCommandHandler>.Instance);

        var pix = await createPixHandler.Handle(
            new CreatePixPaymentForOrderCommand(order.OrderId),
            CancellationToken.None);

        var processor = CreateProcessor(cartDb, ordersDb, paymentsDb, inventoryDb);
        var first = await processor.ProcessAsync(CancellationToken.None);
        var second = await processor.ProcessAsync(CancellationToken.None);

        first.ExpiredCheckoutSessions.Should().BeGreaterThan(0);
        first.ExpiredOrders.Should().BeGreaterThan(0);
        first.ExpiredPixPayments.Should().BeGreaterThan(0);

        second.ExpiredCheckoutSessions.Should().Be(0);
        second.ExpiredOrders.Should().Be(0);
        second.ExpiredPixPayments.Should().Be(0);

        var refreshedSession = await cartDb.CheckoutSessions.FindAsync(session.Id);
        refreshedSession!.Status.Should().Be(CartCheckout.Domain.Enums.CheckoutSessionStatus.Expired);

        var refreshedOrder = await ordersDb.Orders.FindAsync(order.OrderId);
        refreshedOrder!.Status.Should().Be(OrderStatus.Expired);

        var refreshedPix = await paymentsDb.PixPayments.FindAsync(pix.Payment.PaymentId);
        refreshedPix!.Status.Should().Be(PixPaymentStatus.Expired);

        await using var verifyInventoryDb = CreateInventoryContext();
        var inventory = await verifyInventoryDb.InventoryItems.AsNoTracking()
            .SingleAsync(i => i.SkuId == skuId);
        inventory.QuantityReserved.Should().Be(0);
        inventory.AvailableQuantity.Should().Be(10);
    }
}
