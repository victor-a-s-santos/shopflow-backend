using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Vls.Shopflow.Orders.Application.CommandHandlers;
using Vls.Shopflow.Orders.Application.Commands;
using Vls.Shopflow.Orders.Application.DataTransferObjects;
using Vls.Shopflow.Orders.Application.Interfaces;
using Vls.Shopflow.Orders.Application.Options;
using Vls.Shopflow.Orders.Application.Repositories;
using Vls.Shopflow.Orders.Domain.Entities;
using Vls.Shopflow.Orders.Domain.Enums;
using Vls.Shopflow.Orders.Domain.Exceptions;

namespace Vls.Shopflow.Orders.UnitTests.Application;

public sealed class OrderFulfillmentCommandHandlerTests
{
    [Fact]
    public async Task Ship_WhenPendingPayment_Throws()
    {
        var order = CreateOrder();
        var repo = MockRepo(order);
        var sut = new ShipOrderFulfillmentCommandHandler(
            repo.Object,
            Mock.Of<IAdminOrderPixPaymentReader>(),
            MockBatchRepo(),
            Mock.Of<IOrdersUnitOfWork>(),
            Mock.Of<IOrderEmailIntentRepository>(),
            Fulfillment());

        var act = () => sut.Handle(
            new ShipOrderFulfillmentCommand(order.Id, Guid.NewGuid()),
            CancellationToken.None);

        await act.Should().ThrowAsync<OrderNotPaidForShipmentException>();
    }

    [Fact]
    public async Task Deliver_BeforeShip_Throws()
    {
        var order = CreateOrder();
        order.MarkAsPaid();
        var repo = MockRepo(order);
        var sut = new DeliverOrderFulfillmentCommandHandler(
            repo.Object,
            Mock.Of<IAdminOrderPixPaymentReader>(),
            MockBatchRepo(),
            Mock.Of<IOrdersUnitOfWork>(),
            Mock.Of<IOrderEmailIntentRepository>(),
            Fulfillment());

        var act = () => sut.Handle(
            new DeliverOrderFulfillmentCommand(order.Id, Guid.NewGuid()),
            CancellationToken.None);

        await act.Should().ThrowAsync<OrderMustBeShippedBeforeDeliveredException>();
    }

    [Fact]
    public async Task Ship_WhenMissing_ThrowsNotFound()
    {
        var repo = new Mock<IOrderRepository>();
        repo.Setup(x => x.GetByIdWithItemsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var sut = new ShipOrderFulfillmentCommandHandler(
            repo.Object,
            Mock.Of<IAdminOrderPixPaymentReader>(),
            MockBatchRepo(),
            Mock.Of<IOrdersUnitOfWork>(),
            Mock.Of<IOrderEmailIntentRepository>(),
            Fulfillment());

        var act = () => sut.Handle(
            new ShipOrderFulfillmentCommand(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        await act.Should().ThrowAsync<OrderNotFoundException>();
    }

    [Fact]
    public async Task Ship_WhenPaid_ReturnsUpdatedDetail()
    {
        var order = CreateOrder();
        order.MarkAsPaid();
        var repo = MockRepo(order);
        var uow = new Mock<IOrdersUnitOfWork>();
        var paymentReader = new Mock<IAdminOrderPixPaymentReader>();
        paymentReader.Setup(x => x.GetLatestByOrderIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AdminOrderPaymentSummaryDto?)null);

        var sut = new ShipOrderFulfillmentCommandHandler(
            repo.Object,
            paymentReader.Object,
            MockBatchRepo(),
            uow.Object,
            Mock.Of<IOrderEmailIntentRepository>(),
            Fulfillment());
        var result = await sut.Handle(
            new ShipOrderFulfillmentCommand(order.Id, Guid.NewGuid(), "Carrier", "T1"),
            CancellationToken.None);

        result.FulfillmentStatus.Should().Be("Shipped");
        result.TrackingCode.Should().Be("T1");
        uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateInternalNote_PersistsNote()
    {
        var order = CreateOrder();
        var repo = MockRepo(order);
        var uow = new Mock<IOrdersUnitOfWork>();
        var paymentReader = new Mock<IAdminOrderPixPaymentReader>();
        paymentReader.Setup(x => x.GetLatestByOrderIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AdminOrderPaymentSummaryDto?)null);

        var sut = new UpdateOrderInternalNoteCommandHandler(
            repo.Object,
            paymentReader.Object,
            MockBatchRepo(),
            uow.Object,
            Fulfillment());
        var result = await sut.Handle(
            new UpdateOrderInternalNoteCommand(order.Id, "Segurar"),
            CancellationToken.None);

        result.InternalOrderNote.Should().Be("Segurar");
        order.InternalOrderNote.Should().Be("Segurar");
    }

    [Fact]
    public async Task ConfirmStock_WhenPaidAwaitingShipment_FillsFieldsAndAdminId()
    {
        var order = CreateOrder();
        order.MarkAsPaid();
        var adminId = Guid.NewGuid();
        var repo = MockRepo(order);
        var uow = new Mock<IOrdersUnitOfWork>();
        var paymentReader = new Mock<IAdminOrderPixPaymentReader>();
        paymentReader.Setup(x => x.GetLatestByOrderIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AdminOrderPaymentSummaryDto?)null);

        var sut = new ConfirmOrderStockCommandHandler(
            repo.Object,
            paymentReader.Object,
            MockBatchRepo(),
            uow.Object,
            Mock.Of<IOrderEmailIntentRepository>(),
            Fulfillment(requireStockConfirmation: true));

        var result = await sut.Handle(
            new ConfirmOrderStockCommand(order.Id, adminId, "Peça no fornecedor"),
            CancellationToken.None);

        order.StockConfirmedAt.Should().NotBeNull();
        order.StockConfirmedByAdminUserId.Should().Be(adminId);
        order.StockConfirmationNote.Should().Be("Peça no fornecedor");
        result.StockConfirmedAt.Should().Be(order.StockConfirmedAt);
        result.StockConfirmedByAdminUserId.Should().Be(adminId);
        result.CanConfirmStock.Should().BeFalse();
        result.CanMarkAsSeparated.Should().BeTrue();
        uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmStock_WhenAlreadyConfirmed_IsIdempotentAndDoesNotEnqueueAgain()
    {
        var order = CreateOrder();
        order.MarkAsPaid();
        var firstAdmin = Guid.NewGuid();
        order.ConfirmStock(firstAdmin, "Primeira");
        var firstAt = order.StockConfirmedAt;

        var intents = new Mock<IOrderEmailIntentRepository>();
        var sut = new ConfirmOrderStockCommandHandler(
            MockRepo(order).Object,
            Mock.Of<IAdminOrderPixPaymentReader>(),
            MockBatchRepo(),
            Mock.Of<IOrdersUnitOfWork>(),
            intents.Object,
            Fulfillment(requireStockConfirmation: true));

        await sut.Handle(new ConfirmOrderStockCommand(order.Id, Guid.NewGuid(), "Segunda"), CancellationToken.None);

        order.StockConfirmedAt.Should().Be(firstAt);
        order.StockConfirmedByAdminUserId.Should().Be(firstAdmin);
        order.StockConfirmationNote.Should().Be("Primeira");
        intents.Verify(
            x => x.EnsurePendingAsync(It.IsAny<OrderEmailIntent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ConfirmStock_WhenPendingPayment_Throws()
    {
        var order = CreateOrder();
        var sut = new ConfirmOrderStockCommandHandler(
            MockRepo(order).Object,
            Mock.Of<IAdminOrderPixPaymentReader>(),
            MockBatchRepo(),
            Mock.Of<IOrdersUnitOfWork>(),
            Mock.Of<IOrderEmailIntentRepository>(),
            Fulfillment());

        var act = () => sut.Handle(
            new ConfirmOrderStockCommand(order.Id, Guid.NewGuid()),
            CancellationToken.None);

        await act.Should().ThrowAsync<OrderMustBePaidBeforeStockConfirmationException>();
    }

    [Fact]
    public async Task ConfirmStock_WhenDeliveredWithoutConfirmation_Throws()
    {
        var order = CreateOrder();
        order.MarkAsPaid();
        order.MarkAsShipped(Guid.NewGuid());
        order.MarkAsDelivered(Guid.NewGuid());

        var sut = new ConfirmOrderStockCommandHandler(
            MockRepo(order).Object,
            Mock.Of<IAdminOrderPixPaymentReader>(),
            MockBatchRepo(),
            Mock.Of<IOrdersUnitOfWork>(),
            Mock.Of<IOrderEmailIntentRepository>(),
            Fulfillment());

        var act = () => sut.Handle(
            new ConfirmOrderStockCommand(order.Id, Guid.NewGuid()),
            CancellationToken.None);

        await act.Should().ThrowAsync<OrderCannotConfirmStockAfterDeliveredException>();
    }

    [Fact]
    public async Task Ship_WhenRequireStockConfirmationAndNotConfirmed_Throws()
    {
        var order = CreateOrder();
        order.MarkAsPaid();
        var sut = new ShipOrderFulfillmentCommandHandler(
            MockRepo(order).Object,
            Mock.Of<IAdminOrderPixPaymentReader>(),
            MockBatchRepo(),
            Mock.Of<IOrdersUnitOfWork>(),
            Mock.Of<IOrderEmailIntentRepository>(),
            Fulfillment(requireStockConfirmation: true));

        var act = () => sut.Handle(
            new ShipOrderFulfillmentCommand(order.Id, Guid.NewGuid()),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<OrderStockConfirmationRequiredException>();
        ex.Which.Code.Should().Be("ORDER_STOCK_CONFIRMATION_REQUIRED");
    }

    [Fact]
    public async Task Ship_WhenRequireStockConfirmationAndConfirmed_Succeeds()
    {
        var order = CreateOrder();
        order.MarkAsPaid();
        order.ConfirmStock(Guid.NewGuid());
        var sut = new ShipOrderFulfillmentCommandHandler(
            MockRepo(order).Object,
            Mock.Of<IAdminOrderPixPaymentReader>(),
            MockBatchRepo(),
            Mock.Of<IOrdersUnitOfWork>(),
            Mock.Of<IOrderEmailIntentRepository>(),
            Fulfillment(requireStockConfirmation: true));

        var result = await sut.Handle(
            new ShipOrderFulfillmentCommand(order.Id, Guid.NewGuid(), "Carrier", "T2"),
            CancellationToken.None);

        result.FulfillmentStatus.Should().Be("Shipped");
        result.StockConfirmedAt.Should().NotBeNull();
    }

    private static IOptions<FulfillmentOptions> Fulfillment(bool requireStockConfirmation = false)
        => Options.Create(new FulfillmentOptions { RequireStockConfirmation = requireStockConfirmation });

    private static IDeliveryBatchRepository MockBatchRepo()
    {
        var mock = new Mock<IDeliveryBatchRepository>();
        mock.Setup(x => x.FindMembershipByOrderIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DeliveryBatchMembership?)null);
        return mock.Object;
    }

    private static Mock<IOrderRepository> MockRepo(Order order)
    {
        var repo = new Mock<IOrderRepository>();
        repo.Setup(x => x.GetByIdWithItemsAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        return repo;
    }

    private static Order CreateOrder()
    {
        var item = OrderItem.Create(Guid.NewGuid(), "Produto", "SKU-1", 1, 50m);
        var order = Order.CreatePendingPayment(
            Guid.NewGuid(),
            "Cliente",
            "c@test.com",
            "11999999999",
            "01001000",
            "Rua A",
            "10",
            null,
            "Centro",
            "São Paulo",
            "SP",
            50m,
            null,
            50m,
            [item]);
        order.AssignOrderNumber(30001);
        return order;
    }
}
