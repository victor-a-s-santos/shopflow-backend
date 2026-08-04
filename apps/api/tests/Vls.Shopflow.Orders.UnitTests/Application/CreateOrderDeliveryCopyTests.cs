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

namespace Vls.Shopflow.Orders.UnitTests.Application;

public sealed class CreateOrderDeliveryCopyTests
{
    [Fact]
    public async Task CreateOrder_CopiesDeliveryFieldsFromCheckoutSession()
    {
        var sessionId = Guid.NewGuid();
        var preferredDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        var snapshot = new CheckoutSessionSnapshot(
            sessionId,
            "Pending",
            "João Silva",
            "joao@email.com",
            "11999999999",
            "01001000",
            "Rua Exemplo",
            "123",
            null,
            "Centro",
            "São Paulo",
            "SP",
            100m,
            null,
            100m,
            [
                new CheckoutSessionItemSnapshot(
                    Guid.NewGuid(), "Camiseta", "SKU-1", 1, 100m, 100m,
                    "Unit", null, null, null, "peça(s)", false, null, null, null)
            ],
            "ExcursionBus",
            preferredDate,
            "Enviar junto");

        Order? saved = null;
        var reader = new Mock<ICheckoutSessionReader>();
        reader.Setup(x => x.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(snapshot);

        var repo = new Mock<IOrderRepository>();
        repo.Setup(x => x.GetByCheckoutSessionIdWithItemsAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);
        repo.Setup(x => x.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Callback<Order, CancellationToken>((o, _) => saved = o)
            .Returns(Task.CompletedTask);

        var orderNumbers = new Mock<IOrderNumberGenerator>();
        orderNumbers.Setup(x => x.NextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(40001);

        var hasher = new Mock<IGuestOrderAccessTokenHasher>();
        hasher.Setup(x => x.GenerateRawToken()).Returns("tok");
        hasher.Setup(x => x.Hash("tok")).Returns("hash");

        var handler = new CreateOrderFromCheckoutSessionCommandHandler(
            reader.Object,
            repo.Object,
            orderNumbers.Object,
            Mock.Of<IGuestOrderAccessTokenRepository>(),
            hasher.Object,
            Mock.Of<IOrdersUnitOfWork>(),
            Options.Create(new GuestOrderAccessOptions { Enabled = false }),
            Mock.Of<IOrderEmailNotifier>(),
            NullLogger<CreateOrderFromCheckoutSessionCommandHandler>.Instance);

        await handler.Handle(new CreateOrderFromCheckoutSessionCommand(sessionId), CancellationToken.None);

        saved.Should().NotBeNull();
        saved!.PreferredDeliveryMethod.Should().Be(DeliveryMethod.ExcursionBus);
        saved.PreferredDeliveryDate.Should().Be(preferredDate);
        saved.CustomerOrderNote.Should().Be("Enviar junto");
        saved.FulfillmentStatus.Should().Be(FulfillmentStatus.AwaitingShipment);
    }

    [Fact]
    public async Task CreateOrder_WithoutDeliveryFields_StillWorks()
    {
        var sessionId = Guid.NewGuid();
        var snapshot = new CheckoutSessionSnapshot(
            sessionId,
            "Pending",
            "João Silva",
            "joao@email.com",
            "11999999999",
            "01001000",
            "Rua Exemplo",
            "123",
            null,
            "Centro",
            "São Paulo",
            "SP",
            100m,
            null,
            100m,
            [
                new CheckoutSessionItemSnapshot(
                    Guid.NewGuid(), "Camiseta", "SKU-1", 1, 100m, 100m,
                    "Unit", null, null, null, "peça(s)", false, null, null, null)
            ]);

        Order? saved = null;
        var reader = new Mock<ICheckoutSessionReader>();
        reader.Setup(x => x.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(snapshot);

        var repo = new Mock<IOrderRepository>();
        repo.Setup(x => x.GetByCheckoutSessionIdWithItemsAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);
        repo.Setup(x => x.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Callback<Order, CancellationToken>((o, _) => saved = o)
            .Returns(Task.CompletedTask);

        var orderNumbers = new Mock<IOrderNumberGenerator>();
        orderNumbers.Setup(x => x.NextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(40002);

        var hasher = new Mock<IGuestOrderAccessTokenHasher>();
        hasher.Setup(x => x.GenerateRawToken()).Returns("tok");
        hasher.Setup(x => x.Hash("tok")).Returns("hash");

        var handler = new CreateOrderFromCheckoutSessionCommandHandler(
            reader.Object,
            repo.Object,
            orderNumbers.Object,
            Mock.Of<IGuestOrderAccessTokenRepository>(),
            hasher.Object,
            Mock.Of<IOrdersUnitOfWork>(),
            Options.Create(new GuestOrderAccessOptions { Enabled = false }),
            Mock.Of<IOrderEmailNotifier>(),
            NullLogger<CreateOrderFromCheckoutSessionCommandHandler>.Instance);

        await handler.Handle(new CreateOrderFromCheckoutSessionCommand(sessionId), CancellationToken.None);

        saved.Should().NotBeNull();
        saved!.PreferredDeliveryMethod.Should().BeNull();
        saved.CustomerOrderNote.Should().BeNull();
        saved.FulfillmentStatus.Should().Be(FulfillmentStatus.AwaitingShipment);
    }
}
