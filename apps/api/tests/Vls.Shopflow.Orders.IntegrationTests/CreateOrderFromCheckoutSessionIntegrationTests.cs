using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vls.Shopflow.CartCheckout.Domain.Entities;
using Vls.Shopflow.CartCheckout.Infrastructure;
using Vls.Shopflow.Orders.Application.CommandHandlers;
using Vls.Shopflow.Orders.Application.Commands;
using Vls.Shopflow.Orders.Application.Interfaces;
using Vls.Shopflow.Orders.Application.Models;
using Vls.Shopflow.Orders.Application.Options;
using Vls.Shopflow.Orders.Application.QueryHandlers;
using Vls.Shopflow.Orders.Application.Repositories;
using Vls.Shopflow.Orders.Domain.Enums;
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

    private static readonly GuestOrderAccessOptions GuestOptions = new()
    {
        Enabled = true,
        TokenTtlDays = 30,
        TokenHashSecret = "integration-test-guest-order-secret"
    };

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
        GetOrderByIdQueryHandler GetByIdHandler,
        GetGuestOrderStatusQueryHandler GuestStatusHandler,
        GuestOrderAccessTokenHasher Hasher)
        CreateHandlers(OrdersDbContext ordersDb, CartCheckoutDbContext cartCheckoutDb)
    {
        var orderRepository = new OrderRepository(ordersDb);
        var guestTokenRepository = new GuestOrderAccessTokenRepository(ordersDb);
        var unitOfWork = new OrdersUnitOfWork(ordersDb);
        var reader = new CheckoutSessionReader(cartCheckoutDb);
        var hasher = new GuestOrderAccessTokenHasher(Options.Create(GuestOptions));
        var options = Options.Create(GuestOptions);

        return (
            new CreateOrderFromCheckoutSessionCommandHandler(
                reader,
                orderRepository,
                new PostgresOrderNumberGenerator(ordersDb),
                guestTokenRepository,
                hasher,
                unitOfWork,
                options,
                new OrderEmailIntentRepository(ordersDb),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<CreateOrderFromCheckoutSessionCommandHandler>.Instance),
            new GetOrderByIdQueryHandler(orderRepository),
            new GetGuestOrderStatusQueryHandler(
                new Vls.Shopflow.Orders.Application.Services.GuestOrderAccessGate(
                    guestTokenRepository,
                    hasher,
                    orderRepository,
                    options),
                new NullOrderPixPaymentStatusReader(),
                new StubCustomerAccountPort(),
                unitOfWork,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<GetGuestOrderStatusQueryHandler>.Instance),
            hasher);
    }

    private sealed class StubCustomerAccountPort : ICustomerAccountPort
    {
        public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken)
            => Task.FromResult(false);

        public Task<CustomerAccountCreateResult> RegisterAsync(
            string email,
            string password,
            string fullName,
            string? phone,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task SignInAsync(Guid customerUserId, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    [Fact]
    public async Task CreateOrderFromCheckoutSession_PersistsPendingPaymentOrderAndGuestTokenHashOnly()
    {
        if (!await CanConnectAsync())
            return;

        var sessionId = await SeedPendingCheckoutSessionAsync();

        await using var ordersDb = CreateOrdersContext();
        await ordersDb.Database.MigrateAsync();

        await using var cartCheckoutDb = CreateCartCheckoutContext();
        var (createHandler, getByIdHandler, guestStatusHandler, hasher) = CreateHandlers(ordersDb, cartCheckoutDb);

        var created = await createHandler.Handle(
            new CreateOrderFromCheckoutSessionCommand(sessionId),
            CancellationToken.None);

        created.Status.Should().Be("PendingPayment");
        created.CheckoutSessionId.Should().Be(sessionId);
        created.GuestAccessToken.Should().NotBeNullOrWhiteSpace();
        created.GuestAccessTokenExpiresAt.Should().NotBeNull();

        var stored = await ordersDb.GuestOrderAccessTokens
            .AsNoTracking()
            .SingleAsync(t => t.OrderId == created.OrderId);

        stored.TokenHash.Should().Be(hasher.Hash(created.GuestAccessToken!));
        stored.TokenHash.Should().NotBe(created.GuestAccessToken);

        var fetched = await getByIdHandler.Handle(
            new GetOrderByIdQuery(created.OrderId),
            CancellationToken.None);

        fetched.OrderId.Should().Be(created.OrderId);
        fetched.GuestAccessToken.Should().BeNull();
        fetched.Items.Should().ContainSingle(i => i.SkuCode == "SKU-INT");

        var status = await guestStatusHandler.Handle(
            new GetGuestOrderStatusQuery(created.OrderId, created.GuestAccessToken),
            CancellationToken.None);

        status.OrderStatus.Should().Be("PendingPayment");
        status.Customer.Email.Should().Contain("***");

        var denied = () => guestStatusHandler.Handle(
            new GetGuestOrderStatusQuery(created.OrderId, "wrong-token"),
            CancellationToken.None);
        await denied.Should().ThrowAsync<GuestOrderAccessDeniedException>();
    }

    [Fact]
    public async Task CreateOrderFromCheckoutSession_PersistsExactlyOneCreatedIntent()
    {
        if (!await CanConnectAsync())
            return;

        var sessionId = await SeedPendingCheckoutSessionAsync();

        await using var ordersDb = CreateOrdersContext();
        await ordersDb.Database.MigrateAsync();
        await using var cartCheckoutDb = CreateCartCheckoutContext();
        var (createHandler, _, _, _) = CreateHandlers(ordersDb, cartCheckoutDb);

        var created = await createHandler.Handle(
            new CreateOrderFromCheckoutSessionCommand(sessionId),
            CancellationToken.None);

        var intents = await ordersDb.EmailIntents
            .AsNoTracking()
            .Where(i => i.OrderId == created.OrderId)
            .ToListAsync();

        intents.Should().ContainSingle();
        intents[0].Type.Should().Be(OrderEmailIntentType.OrderCreated);
        intents[0].Status.Should().Be(OrderEmailIntentStatus.Pending);
        intents[0].IdempotencyKey.Should().Be($"order:{created.OrderId:D}:created");

        var payload = OrderEmailIntentPayloadJson.Deserialize(intents[0].PayloadJson);
        payload.CustomerEmail.Should().Be("integracao@shopflow.test");
        payload.CustomerName.Should().Be("Cliente Integração");
        payload.GuestAccessToken.Should().NotBeNullOrWhiteSpace();
        payload.GuestAccessToken!.Length.Should().BeGreaterThan(8);
        intents[0].PayloadJson.Should().NotContain("<html");
    }

    [Fact]
    public async Task CreateOrderFromCheckoutSession_WhenSaveChangesFails_DoesNotPersistOrderOrIntent()
    {
        if (!await CanConnectAsync())
            return;

        var sessionId = await SeedPendingCheckoutSessionAsync();

        await using var ordersDb = CreateOrdersContext();
        await ordersDb.Database.MigrateAsync();
        await using var cartCheckoutDb = CreateCartCheckoutContext();

        var throwingUow = new ThrowingOrdersUnitOfWork();
        var handler = new CreateOrderFromCheckoutSessionCommandHandler(
            new CheckoutSessionReader(cartCheckoutDb),
            new OrderRepository(ordersDb),
            new PostgresOrderNumberGenerator(ordersDb),
            new GuestOrderAccessTokenRepository(ordersDb),
            new GuestOrderAccessTokenHasher(Options.Create(GuestOptions)),
            throwingUow,
            Options.Create(GuestOptions),
            new OrderEmailIntentRepository(ordersDb),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<CreateOrderFromCheckoutSessionCommandHandler>.Instance);

        var act = () => handler.Handle(
            new CreateOrderFromCheckoutSessionCommand(sessionId),
            CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();

        var trackedOrder = ordersDb.Orders.Local.Single(o => o.CheckoutSessionId == sessionId);
        await using var verify = CreateOrdersContext();
        (await verify.Orders.AnyAsync(o => o.Id == trackedOrder.Id)).Should().BeFalse();
        (await verify.EmailIntents.AnyAsync(i => i.OrderId == trackedOrder.Id)).Should().BeFalse();
    }

    private sealed class ThrowingOrdersUnitOfWork : IOrdersUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default)
            => throw new InvalidOperationException("db down");
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

        var (createHandler, _, _, _) = CreateHandlers(ordersDb, cartCheckoutDb);

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

        var (createHandler, _, _, _) = CreateHandlers(ordersDb, cartCheckoutDb);

        var act = () => createHandler.Handle(
            new CreateOrderFromCheckoutSessionCommand(Guid.NewGuid()),
            CancellationToken.None);

        await act.Should().ThrowAsync<CheckoutSessionNotFoundForOrderException>();
    }
}
