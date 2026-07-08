using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Vls.Shopflow.Orders.Application.CommandHandlers;
using Vls.Shopflow.Orders.Application.Commands;
using Vls.Shopflow.Orders.Application.Interfaces;
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

    [Fact]
    public async Task Handle_WithValidCheckoutSession_CreatesPendingPaymentOrder()
    {
        var sessionId = Guid.NewGuid();
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

        var uow = new Mock<IOrdersUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new CreateOrderFromCheckoutSessionCommandHandler(
            reader.Object,
            repository.Object,
            uow.Object,
            NullLogger<CreateOrderFromCheckoutSessionCommandHandler>.Instance);

        var result = await handler.Handle(
            new CreateOrderFromCheckoutSessionCommand(sessionId),
            CancellationToken.None);

        result.OrderId.Should().NotBeEmpty();
        result.CheckoutSessionId.Should().Be(sessionId);
        result.Status.Should().Be("PendingPayment");
        result.Total.Should().Be(200m);
        result.Items.Should().ContainSingle();

        captured.Should().NotBeNull();
        captured!.Status.Should().Be(OrderStatus.PendingPayment);
        captured.CheckoutSessionId.Should().Be(sessionId);
        captured.CustomerEmail.Should().Be("joao@email.com");
    }

    [Fact]
    public async Task Handle_WhenCheckoutSessionMissing_ThrowsNotFound()
    {
        var sessionId = Guid.NewGuid();
        var reader = new Mock<ICheckoutSessionReader>();
        reader.Setup(x => x.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CheckoutSessionSnapshot?)null);

        var handler = new CreateOrderFromCheckoutSessionCommandHandler(
            reader.Object,
            Mock.Of<IOrderRepository>(),
            Mock.Of<IOrdersUnitOfWork>(),
            NullLogger<CreateOrderFromCheckoutSessionCommandHandler>.Instance);

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

        var handler = new CreateOrderFromCheckoutSessionCommandHandler(
            reader.Object,
            repository.Object,
            Mock.Of<IOrdersUnitOfWork>(),
            NullLogger<CreateOrderFromCheckoutSessionCommandHandler>.Instance);

        var act = () => handler.Handle(
            new CreateOrderFromCheckoutSessionCommand(sessionId),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidCheckoutSessionForOrderException>();
    }

    [Fact]
    public async Task Handle_WhenOrderAlreadyExists_ThrowsConflict()
    {
        var sessionId = Guid.NewGuid();
        var existingOrderId = Guid.NewGuid();
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

        var handler = new CreateOrderFromCheckoutSessionCommandHandler(
            Mock.Of<ICheckoutSessionReader>(),
            repository.Object,
            Mock.Of<IOrdersUnitOfWork>(),
            NullLogger<CreateOrderFromCheckoutSessionCommandHandler>.Instance);

        var act = () => handler.Handle(
            new CreateOrderFromCheckoutSessionCommand(sessionId),
            CancellationToken.None);

        await act.Should().ThrowAsync<OrderAlreadyExistsForCheckoutSessionException>()
            .Where(ex => ex.ExistingOrderId == existing.Id);
    }
}
