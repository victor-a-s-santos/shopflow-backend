using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.CartCheckout.Domain.Entities;
using Vls.Shopflow.CartCheckout.Infrastructure;
using Vls.Shopflow.Orders.Application.CommandHandlers;
using Vls.Shopflow.Orders.Application.Commands;
using Vls.Shopflow.Orders.Application.QueryHandlers;
using Vls.Shopflow.Orders.Domain.Exceptions;
using Vls.Shopflow.Orders.Infrastructure;
using Vls.Shopflow.Orders.Infrastructure.Repositories;
using Vls.Shopflow.Orders.Infrastructure.Services;
using Vls.Shopflow.Orders.Infrastructure.UnitOfWork;

namespace Vls.Shopflow.Orders.IntegrationTests;

public sealed class CreateOrderFromCheckoutSessionIntegrationTests
{
    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("SHOPFLOW_TEST_DB")
        ?? "Host=localhost;Port=5432;Database=shopflow;Username=postgres;Password=postgres";

    private static async Task<bool> CanConnectAsync()
    {
        try
        {
            await using var db = CreateOrdersContext();
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

    private static async Task<Guid> SeedPendingCheckoutSessionAsync()
    {
        await using var db = CreateCartCheckoutContext();
        await db.Database.MigrateAsync();

        var skuId = Guid.NewGuid();
        var item = CheckoutSessionItem.Create(
            Guid.NewGuid(),
            "Produto Integração",
            "produto-integracao",
            skuId,
            "SKU-INT",
            1,
            99.90m,
            Guid.NewGuid());

        var session = CheckoutSession.CreatePending(
            "Cliente Integração",
            "integracao@shopflow.test",
            "11999990000",
            "01001000",
            "Rua Teste",
            "100",
            null,
            "Centro",
            "São Paulo",
            "SP",
            DateTimeOffset.UtcNow.AddMinutes(15),
            new[] { item });

        db.CheckoutSessions.Add(session);
        await db.SaveChangesAsync();
        return session.Id;
    }

    private static (
        CreateOrderFromCheckoutSessionCommandHandler CreateHandler,
        GetOrderByIdQueryHandler GetByIdHandler)
        CreateHandlers(OrdersDbContext ordersDb, CartCheckoutDbContext cartCheckoutDb)
    {
        var orderRepository = new OrderRepository(ordersDb);
        var unitOfWork = new OrdersUnitOfWork(ordersDb);
        var reader = new CheckoutSessionReader(cartCheckoutDb);

        return (
            new CreateOrderFromCheckoutSessionCommandHandler(
                reader,
                orderRepository,
                unitOfWork,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<CreateOrderFromCheckoutSessionCommandHandler>.Instance),
            new GetOrderByIdQueryHandler(orderRepository));
    }

    [Fact]
    public async Task CreateOrderFromCheckoutSession_PersistsPendingPaymentOrder()
    {
        if (!await CanConnectAsync())
            return;

        var sessionId = await SeedPendingCheckoutSessionAsync();

        await using var ordersDb = CreateOrdersContext();
        await ordersDb.Database.MigrateAsync();

        await using var cartCheckoutDb = CreateCartCheckoutContext();
        var (createHandler, getByIdHandler) = CreateHandlers(ordersDb, cartCheckoutDb);

        var created = await createHandler.Handle(
            new CreateOrderFromCheckoutSessionCommand(sessionId),
            CancellationToken.None);

        created.Status.Should().Be("PendingPayment");
        created.CheckoutSessionId.Should().Be(sessionId);
        created.Total.Should().Be(99.90m);

        var fetched = await getByIdHandler.Handle(
            new GetOrderByIdQuery(created.OrderId),
            CancellationToken.None);

        fetched.OrderId.Should().Be(created.OrderId);
        fetched.Items.Should().ContainSingle(i => i.SkuCode == "SKU-INT");
    }

    [Fact]
    public async Task CreateOrderFromCheckoutSession_DuplicateReturnsConflict()
    {
        if (!await CanConnectAsync())
            return;

        var sessionId = await SeedPendingCheckoutSessionAsync();

        await using var ordersDb = CreateOrdersContext();
        await ordersDb.Database.MigrateAsync();
        await using var cartCheckoutDb = CreateCartCheckoutContext();

        var (createHandler, _) = CreateHandlers(ordersDb, cartCheckoutDb);

        await createHandler.Handle(
            new CreateOrderFromCheckoutSessionCommand(sessionId),
            CancellationToken.None);

        var act = () => createHandler.Handle(
            new CreateOrderFromCheckoutSessionCommand(sessionId),
            CancellationToken.None);

        await act.Should().ThrowAsync<OrderAlreadyExistsForCheckoutSessionException>();
    }

    [Fact]
    public async Task CreateOrderFromCheckoutSession_MissingSessionReturnsNotFound()
    {
        if (!await CanConnectAsync())
            return;

        await using var ordersDb = CreateOrdersContext();
        await ordersDb.Database.MigrateAsync();
        await using var cartCheckoutDb = CreateCartCheckoutContext();
        await cartCheckoutDb.Database.MigrateAsync();

        var (createHandler, _) = CreateHandlers(ordersDb, cartCheckoutDb);

        var act = () => createHandler.Handle(
            new CreateOrderFromCheckoutSessionCommand(Guid.NewGuid()),
            CancellationToken.None);

        await act.Should().ThrowAsync<CheckoutSessionNotFoundForOrderException>();
    }
}
