using FluentAssertions;
using Moq;
using Vls.Shopflow.Orders.Application.CommandHandlers;
using Vls.Shopflow.Orders.Application.Commands;
using Vls.Shopflow.Orders.Application.DataTransferObjects;
using Vls.Shopflow.Orders.Application.Interfaces;
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
            Mock.Of<IOrderEmailIntentRepository>());

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
            Mock.Of<IOrderEmailIntentRepository>());

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
            Mock.Of<IOrderEmailIntentRepository>());

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
            Mock.Of<IOrderEmailIntentRepository>());
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
            uow.Object);
        var result = await sut.Handle(
            new UpdateOrderInternalNoteCommand(order.Id, "Segurar"),
            CancellationToken.None);

        result.InternalOrderNote.Should().Be("Segurar");
        order.InternalOrderNote.Should().Be("Segurar");
    }

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
