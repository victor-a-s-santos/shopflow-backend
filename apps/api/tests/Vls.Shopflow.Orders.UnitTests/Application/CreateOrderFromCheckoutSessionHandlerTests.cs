using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Vls.Shopflow.Orders.Application.CommandHandlers;
using Vls.Shopflow.Orders.Application.Commands;
using Vls.Shopflow.Orders.Application.Interfaces;
using Vls.Shopflow.Orders.Application.Options;
using Vls.Shopflow.Orders.Application.Repositories;
using Vls.Shopflow.Orders.Domain.Entities;
using Vls.Shopflow.Orders.Domain.Enums;
using Vls.Shopflow.Orders.Domain.Exceptions;

namespace Vls.Shopflow.Orders.UnitTests.Application;

public sealed class CreateOrderFromCheckoutSessionHandlerTests
{
    private static CheckoutSessionSnapshot PendingSession(Guid sessionId)
        => new(
            sessionId,
            "Pending",
            "João Silva",
            "joao@email.com",
            "11999999999",
            "01001000",
            "Rua Exemplo",
            "123",
            "Apto 10",
            "Centro",
            "São Paulo",
            "SP",
            200m,
            null,
            200m,
            new[]
            {
                new CheckoutSessionItemSnapshot(
                    Guid.NewGuid(),
                    "Camiseta",
                    "SKU-001",
                    2,
                    100m,
                    200m)
            });

    private static CreateOrderFromCheckoutSessionCommandHandler CreateHandler(
        ICheckoutSessionReader reader,
        IOrderRepository repository,
        IOrdersUnitOfWork? uow = null,
        IGuestOrderAccessTokenRepository? tokenRepo = null,
        IGuestOrderAccessTokenHasher? hasher = null,
        GuestOrderAccessOptions? options = null)
    {
        var tokenRepository = tokenRepo ?? Mock.Of<IGuestOrderAccessTokenRepository>();
        var tokenHasher = hasher;
        if (tokenHasher is null)
        {
            var hasherMock = new Mock<IGuestOrderAccessTokenHasher>();
            hasherMock.Setup(x => x.GenerateRawToken()).Returns("raw-guest-token");
            hasherMock.Setup(x => x.Hash("raw-guest-token")).Returns("hashed-guest-token");
            tokenHasher = hasherMock.Object;
        }

        var orderNumbers = new Mock<IOrderNumberGenerator>();
        orderNumbers.Setup(x => x.NextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(10582);

        return new CreateOrderFromCheckoutSessionCommandHandler(
            reader,
            repository,
            orderNumbers.Object,
            tokenRepository,
            tokenHasher,
            uow ?? Mock.Of<IOrdersUnitOfWork>(x =>
                x.SaveChangesAsync(It.IsAny<CancellationToken>()) == Task.FromResult(1)),
            Options.Create(options ?? new GuestOrderAccessOptions
            {
                Enabled = true,
                TokenTtlDays = 30,
                TokenHashSecret = "test-secret"
            }),
            NullLogger<CreateOrderFromCheckoutSessionCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_WithValidCheckoutSession_CreatesPendingPaymentOrderAndGuestToken()
    {
        var sessionId = Guid.NewGuid();
        var reader = new Mock<ICheckoutSessionReader>();
        reader.Setup(x => x.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PendingSession(sessionId));

        Order? captured = null;
        GuestOrderAccessToken? capturedToken = null;
        var repository = new Mock<IOrderRepository>();
        repository.Setup(x => x.GetByCheckoutSessionIdWithItemsAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);
        repository.Setup(x => x.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Callback<Order, CancellationToken>((order, _) => captured = order)
            .Returns(Task.CompletedTask);

        var tokenRepo = new Mock<IGuestOrderAccessTokenRepository>();
        tokenRepo.Setup(x => x.AddAsync(It.IsAny<GuestOrderAccessToken>(), It.IsAny<CancellationToken>()))
            .Callback<GuestOrderAccessToken, CancellationToken>((token, _) => capturedToken = token)
            .Returns(Task.CompletedTask);

        var handler = CreateHandler(reader.Object, repository.Object, tokenRepo: tokenRepo.Object);

        var result = await handler.Handle(
            new CreateOrderFromCheckoutSessionCommand(sessionId),
            CancellationToken.None);

        result.OrderId.Should().NotBeEmpty();
        result.OrderNumber.Should().Be("10582");
        result.CheckoutSessionId.Should().Be(sessionId);
        result.Status.Should().Be("PendingPayment");
        result.GuestAccessToken.Should().Be("raw-guest-token");
        result.GuestAccessTokenExpiresAt.Should().NotBeNull();

        captured.Should().NotBeNull();
        captured!.Status.Should().Be(OrderStatus.PendingPayment);
        captured.OrderNumber.Should().Be(10582);
        captured.CustomerUserId.Should().BeNull();

        capturedToken.Should().NotBeNull();
        capturedToken!.TokenHash.Should().Be("hashed-guest-token");
        capturedToken.TokenHash.Should().NotBe(result.GuestAccessToken);
        capturedToken.OrderId.Should().Be(captured.Id);
    }

    [Fact]
    public async Task Handle_WithCustomerUserId_BindsOrderToCustomer()
    {
        var sessionId = Guid.NewGuid();
        var customerUserId = Guid.NewGuid();
        var reader = new Mock<ICheckoutSessionReader>();
        reader.Setup(x => x.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PendingSession(sessionId));

        Order? captured = null;
        var repository = new Mock<IOrderRepository>();
        repository.Setup(x => x.GetByCheckoutSessionIdWithItemsAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);
        repository.Setup(x => x.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Callback<Order, CancellationToken>((order, _) => captured = order)
            .Returns(Task.CompletedTask);

        var handler = CreateHandler(reader.Object, repository.Object);

        await handler.Handle(
            new CreateOrderFromCheckoutSessionCommand(sessionId, customerUserId),
            CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.CustomerUserId.Should().Be(customerUserId);
    }

    [Fact]
    public async Task Handle_WhenGuestAccessDisabled_DoesNotIssueToken()
    {
        var sessionId = Guid.NewGuid();
        var reader = new Mock<ICheckoutSessionReader>();
        reader.Setup(x => x.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PendingSession(sessionId));

        var repository = new Mock<IOrderRepository>();
        repository.Setup(x => x.GetByCheckoutSessionIdWithItemsAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);
        repository.Setup(x => x.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var tokenRepo = new Mock<IGuestOrderAccessTokenRepository>();

        var handler = CreateHandler(
            reader.Object,
            repository.Object,
            tokenRepo: tokenRepo.Object,
            options: new GuestOrderAccessOptions { Enabled = false });

        var result = await handler.Handle(
            new CreateOrderFromCheckoutSessionCommand(sessionId),
            CancellationToken.None);

        result.GuestAccessToken.Should().BeNull();
        tokenRepo.Verify(
            x => x.AddAsync(It.IsAny<GuestOrderAccessToken>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCheckoutSessionMissing_ThrowsNotFound()
    {
        var sessionId = Guid.NewGuid();
        var reader = new Mock<ICheckoutSessionReader>();
        reader.Setup(x => x.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CheckoutSessionSnapshot?)null);

        var handler = CreateHandler(reader.Object, Mock.Of<IOrderRepository>());

        var act = () => handler.Handle(
            new CreateOrderFromCheckoutSessionCommand(sessionId),
            CancellationToken.None);

        await act.Should().ThrowAsync<CheckoutSessionNotFoundForOrderException>();
    }

    [Fact]
    public async Task Handle_WhenCheckoutSessionNotPending_ThrowsInvalidStatus()
    {
        var sessionId = Guid.NewGuid();
        var canceled = PendingSession(sessionId) with { Status = "Canceled" };

        var reader = new Mock<ICheckoutSessionReader>();
        reader.Setup(x => x.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(canceled);

        var repository = new Mock<IOrderRepository>();
        repository.Setup(x => x.GetByCheckoutSessionIdWithItemsAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var handler = CreateHandler(reader.Object, repository.Object);

        var act = () => handler.Handle(
            new CreateOrderFromCheckoutSessionCommand(sessionId),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidCheckoutSessionForOrderException>();
    }

    [Fact]
    public async Task Handle_WhenOrderAlreadyExists_ThrowsConflict()
    {
        var sessionId = Guid.NewGuid();
        var existing = Order.CreatePendingPayment(
            sessionId,
            "João",
            "joao@email.com",
            "11999999999",
            "01001000",
            "Rua",
            "1",
            null,
            "Centro",
            "São Paulo",
            "SP",
            100m,
            null,
            100m,
            new[] { OrderItem.Create(Guid.NewGuid(), "Item", "SKU", 1, 100m) });

        var repository = new Mock<IOrderRepository>();
        repository.Setup(x => x.GetByCheckoutSessionIdWithItemsAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var handler = CreateHandler(Mock.Of<ICheckoutSessionReader>(), repository.Object);

        var act = () => handler.Handle(
            new CreateOrderFromCheckoutSessionCommand(sessionId),
            CancellationToken.None);

        await act.Should().ThrowAsync<OrderAlreadyExistsForCheckoutSessionException>()
            .Where(ex => ex.ExistingOrderId == existing.Id);
    }
}
