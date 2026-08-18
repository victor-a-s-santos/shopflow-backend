using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vls.Shopflow.CartCheckout.Domain.Entities;
using Vls.Shopflow.CartCheckout.Infrastructure;
using Vls.Shopflow.Orders.Application.CommandHandlers;
using Vls.Shopflow.Orders.Application.Commands;
using Vls.Shopflow.Orders.Application.Options;
using Vls.Shopflow.Orders.Domain.Entities;
using Vls.Shopflow.Orders.Infrastructure;
using Vls.Shopflow.Orders.Infrastructure.Repositories;
using Vls.Shopflow.Orders.Infrastructure.Services;
using Vls.Shopflow.Orders.Infrastructure.UnitOfWork;
using Vls.Shopflow.PaymentsPix.Application.CommandHandlers;
using Vls.Shopflow.PaymentsPix.Application.Commands;
using Vls.Shopflow.PaymentsPix.Application.Options;
using Vls.Shopflow.PaymentsPix.Application.QueryHandlers;
using Vls.Shopflow.PaymentsPix.Domain.Exceptions;
using Vls.Shopflow.PaymentsPix.Infrastructure;
using Vls.Shopflow.PaymentsPix.Infrastructure.Providers;
using Vls.Shopflow.PaymentsPix.Infrastructure.Repositories;
using Vls.Shopflow.PaymentsPix.Infrastructure.Services;
using Vls.Shopflow.PaymentsPix.Infrastructure.UnitOfWork;

namespace Vls.Shopflow.PaymentsPix.IntegrationTests;

public sealed class CreatePixPaymentIntegrationTests
{
    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("SHOPFLOW_TEST_DB")
        ?? "Host=localhost;Port=5432;Database=shopflow;Username=postgres;Password=postgres";

    private static async Task<bool> CanConnectAsync()
    {
        try
        {
            await using var db = CreatePaymentsPixContext();
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

    internal static async Task<Guid> SeedPendingOrderAsync()
    {
        await using var cartDb = CreateCartCheckoutContext();
        await cartDb.Database.MigrateAsync();

        var skuId = Guid.NewGuid();
        var item = CheckoutSessionItem.Create(
            Guid.NewGuid(),
            "Produto Pix",
            "produto-pix",
            skuId,
            "SKU-PIX",
            1,
            49.90m,
            Guid.NewGuid());

        var session = CheckoutSession.CreatePending(
            "Cliente Pix",
            "pix@shopflow.test",
            "11999990000",
            "01001000",
            "Rua Pix",
            "50",
            null,
            "Centro",
            "São Paulo",
            "SP",
            DateTimeOffset.UtcNow.AddMinutes(15),
            new[] { item });

        cartDb.CheckoutSessions.Add(session);
        await cartDb.SaveChangesAsync();

        await using var ordersDb = CreateOrdersContext();
        await ordersDb.Database.MigrateAsync();
        await using var cartCheckoutDb = CreateCartCheckoutContext();

        var createOrderHandler = new CreateOrderFromCheckoutSessionCommandHandler(
            new CheckoutSessionReader(cartCheckoutDb),
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
            new OrderEmailIntentRepository(ordersDb),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<CreateOrderFromCheckoutSessionCommandHandler>.Instance);

        var order = await createOrderHandler.Handle(
            new CreateOrderFromCheckoutSessionCommand(session.Id),
            CancellationToken.None);

        return order.OrderId;
    }

    private static (
        CreatePixPaymentForOrderCommandHandler CreateHandler,
        GetPixPaymentByIdQueryHandler GetByIdHandler,
        GetPixPaymentByOrderIdQueryHandler GetByOrderHandler)
        CreateHandlers(
            PaymentsPixDbContext paymentsDb,
            OrdersDbContext ordersDb)
    {
        var paymentRepository = new PixPaymentRepository(paymentsDb);
        var unitOfWork = new PaymentsPixUnitOfWork(paymentsDb);
        var orderReader = new OrderPaymentReader(ordersDb);
        var provider = new FakePixPaymentProvider();

        return (
            new CreatePixPaymentForOrderCommandHandler(
                orderReader,
                paymentRepository,
                provider,
                unitOfWork,
                Options.Create(new MercadoPagoOptions { PixExpirationMinutes = 30 }),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<CreatePixPaymentForOrderCommandHandler>.Instance),
            new GetPixPaymentByIdQueryHandler(paymentRepository),
            new GetPixPaymentByOrderIdQueryHandler(paymentRepository));
    }

    [Fact]
    public async Task CreatePixPayment_PersistsPendingPayment()
    {
        if (!await CanConnectAsync())
            return;

        var orderId = await SeedPendingOrderAsync();

        await using var paymentsDb = CreatePaymentsPixContext();
        await paymentsDb.Database.MigrateAsync();
        await using var ordersDb = CreateOrdersContext();

        var (createHandler, getByIdHandler, getByOrderHandler) = CreateHandlers(paymentsDb, ordersDb);

        var created = await createHandler.Handle(
            new CreatePixPaymentForOrderCommand(orderId),
            CancellationToken.None);

        created.WasCreated.Should().BeTrue();
        created.Payment.Status.Should().Be("Pending");
        created.Payment.Provider.Should().Be("Fake");
        created.Payment.Amount.Should().Be(49.90m);

        var byId = await getByIdHandler.Handle(
            new Application.Queries.GetPixPaymentByIdQuery(created.Payment.PaymentId),
            CancellationToken.None);

        byId.PaymentId.Should().Be(created.Payment.PaymentId);

        var byOrder = await getByOrderHandler.Handle(
            new Application.Queries.GetPixPaymentByOrderIdQuery(orderId),
            CancellationToken.None);

        byOrder.OrderId.Should().Be(orderId);
    }

    [Fact]
    public async Task CreatePixPayment_DuplicateReturnsExisting()
    {
        if (!await CanConnectAsync())
            return;

        var orderId = await SeedPendingOrderAsync();

        await using var paymentsDb = CreatePaymentsPixContext();
        await paymentsDb.Database.MigrateAsync();
        await using var ordersDb = CreateOrdersContext();

        var (createHandler, _, _) = CreateHandlers(paymentsDb, ordersDb);

        var first = await createHandler.Handle(
            new CreatePixPaymentForOrderCommand(orderId),
            CancellationToken.None);

        var second = await createHandler.Handle(
            new CreatePixPaymentForOrderCommand(orderId),
            CancellationToken.None);

        second.WasCreated.Should().BeFalse();
        second.Payment.PaymentId.Should().Be(first.Payment.PaymentId);
    }

    [Fact]
    public async Task CreatePixPayment_MissingOrderReturnsNotFound()
    {
        if (!await CanConnectAsync())
            return;

        await using var paymentsDb = CreatePaymentsPixContext();
        await paymentsDb.Database.MigrateAsync();
        await using var ordersDb = CreateOrdersContext();
        await ordersDb.Database.MigrateAsync();

        var (createHandler, _, _) = CreateHandlers(paymentsDb, ordersDb);

        var act = () => createHandler.Handle(
            new CreatePixPaymentForOrderCommand(Guid.NewGuid()),
            CancellationToken.None);

        await act.Should().ThrowAsync<OrderNotFoundForPixPaymentException>();
    }
}
