using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Vls.Shopflow.Orders.Application.CommandHandlers;
using Vls.Shopflow.Orders.Application.Commands;
using Vls.Shopflow.Orders.Application.DataTransferObjects;
using Vls.Shopflow.Orders.Application.Interfaces;
using Vls.Shopflow.Orders.Application.Repositories;
using Vls.Shopflow.Orders.Domain.Constants;
using Vls.Shopflow.Orders.Domain.Entities;
using Vls.Shopflow.Orders.Domain.Enums;
using Vls.Shopflow.Orders.Domain.Exceptions;

namespace Vls.Shopflow.Orders.UnitTests.Application;

public sealed class DeliveryBatchCommandHandlerTests
{
    private static Order PaidOrder(
        Guid customerUserId,
        string street = "Rua A",
        long orderNumber = 10001,
        string? internalNote = null)
    {
        var item = OrderItem.Create(Guid.NewGuid(), "Produto", "SKU-1", 1, 80m);
        var order = Order.CreatePendingPayment(
            Guid.NewGuid(),
            "Loja",
            "loja@test.com",
            "11999999999",
            "01001000",
            street,
            "10",
            null,
            "Centro",
            "São Paulo",
            "SP",
            80m,
            null,
            80m,
            [item],
            customerUserId);
        order.AssignOrderNumber(orderNumber);
        order.MarkAsPaid();
        if (internalNote is not null)
            order.SetInternalOrderNote(internalNote);
        return order;
    }

    [Fact]
    public async Task Create_WithAddressMismatchWithoutConfirm_Throws()
    {
        var userId = Guid.NewGuid();
        var a = PaidOrder(userId, "Rua A", 1);
        var b = PaidOrder(userId, "Rua B", 2);
        var sut = CreateCreateHandler([a, b]);

        var act = () => sut.Handle(
            new CreateDeliveryBatchCommand([a.Id, b.Id], Guid.NewGuid(), ConfirmDifferentAddresses: false),
            CancellationToken.None);

        await act.Should().ThrowAsync<DeliveryBatchAddressMismatchException>();
    }

    [Fact]
    public async Task Create_WithAddressMismatchAndConfirm_Succeeds()
    {
        var userId = Guid.NewGuid();
        var a = PaidOrder(userId, "Rua A", 1);
        var b = PaidOrder(userId, "Rua B", 2);
        var sut = CreateCreateHandler([a, b]);

        var result = await sut.Handle(
            new CreateDeliveryBatchCommand(
                [a.Id, b.Id],
                Guid.NewGuid(),
                DeliveryMethod: "Carrier",
                ConfirmDifferentAddresses: true),
            CancellationToken.None);

        result.Status.Should().Be("AwaitingShipment");
        result.OrderCount.Should().Be(2);
        result.HasDifferentDeliveryAddresses.Should().BeTrue();
        result.BatchNumber.Should().Be("30001");
    }

    [Fact]
    public async Task Ship_UpdatesBatchAndAllOrders_WithoutOverwritingOrderNotes()
    {
        var userId = Guid.NewGuid();
        var a = PaidOrder(userId, orderNumber: 1, internalNote: "Nota pedido A");
        var b = PaidOrder(userId, orderNumber: 2, internalNote: "Nota pedido B");
        var batch = DeliveryBatch.CreateAwaitingShipment(
            [a.Id, b.Id], userId, "Loja", "loja@test.com", "11999999999", false, Guid.NewGuid());
        batch.AssignBatchNumber(30050);

        var batchRepo = new Mock<IDeliveryBatchRepository>();
        batchRepo.Setup(x => x.GetByIdWithOrdersAsync(batch.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(batch);

        var orderRepo = new Mock<IOrderRepository>();
        orderRepo.Setup(x => x.GetByIdsWithItemsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([a, b]);

        var paymentReader = new Mock<IAdminOrderPixPaymentReader>();
        paymentReader.Setup(x => x.GetLatestByOrderIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, AdminOrderPaymentSummaryDto>());

        var uow = new Mock<IOrdersUnitOfWork>();
        var sut = new ShipDeliveryBatchCommandHandler(
            batchRepo.Object, orderRepo.Object, paymentReader.Object, uow.Object);

        var result = await sut.Handle(
            new ShipDeliveryBatchCommand(batch.Id, Guid.NewGuid(), "Correios", "BR1", "Nota remessa"),
            CancellationToken.None);

        result.Status.Should().Be("Shipped");
        result.TrackingCode.Should().Be("BR1");
        a.FulfillmentStatus.Should().Be(FulfillmentStatus.Shipped);
        b.FulfillmentStatus.Should().Be(FulfillmentStatus.Shipped);
        a.InternalOrderNote.Should().Be("Nota pedido A");
        b.InternalOrderNote.Should().Be("Nota pedido B");
        batch.InternalNote.Should().Be("Nota remessa");
        uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Deliver_WhenAwaiting_Throws()
    {
        var userId = Guid.NewGuid();
        var a = PaidOrder(userId, orderNumber: 1);
        var b = PaidOrder(userId, orderNumber: 2);
        var batch = DeliveryBatch.CreateAwaitingShipment(
            [a.Id, b.Id], userId, "Loja", "loja@test.com", "11999999999", false, Guid.NewGuid());
        batch.AssignBatchNumber(30051);

        var batchRepo = new Mock<IDeliveryBatchRepository>();
        batchRepo.Setup(x => x.GetByIdWithOrdersAsync(batch.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(batch);
        var orderRepo = new Mock<IOrderRepository>();
        orderRepo.Setup(x => x.GetByIdsWithItemsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([a, b]);

        var sut = new DeliverDeliveryBatchCommandHandler(
            batchRepo.Object,
            orderRepo.Object,
            Mock.Of<IAdminOrderPixPaymentReader>(),
            Mock.Of<IOrdersUnitOfWork>());

        var act = () => sut.Handle(
            new DeliverDeliveryBatchCommand(batch.Id, Guid.NewGuid()),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<DeliveryBatchException>();
        ex.Which.Code.Should().Be(DeliveryBatchErrorCodes.CannotBeDelivered);
    }

    [Fact]
    public async Task Deliver_WhenShipped_UpdatesOrders()
    {
        var userId = Guid.NewGuid();
        var a = PaidOrder(userId, orderNumber: 1);
        var b = PaidOrder(userId, orderNumber: 2);
        a.MarkAsShipped(Guid.NewGuid());
        b.MarkAsShipped(Guid.NewGuid());
        var batch = DeliveryBatch.CreateAwaitingShipment(
            [a.Id, b.Id], userId, "Loja", "loja@test.com", "11999999999", false, Guid.NewGuid());
        batch.AssignBatchNumber(30052);
        batch.MarkAsShipped(Guid.NewGuid(), DeliveryMethod.Carrier, "T");

        var batchRepo = new Mock<IDeliveryBatchRepository>();
        batchRepo.Setup(x => x.GetByIdWithOrdersAsync(batch.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(batch);
        var orderRepo = new Mock<IOrderRepository>();
        orderRepo.Setup(x => x.GetByIdsWithItemsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([a, b]);
        var paymentReader = new Mock<IAdminOrderPixPaymentReader>();
        paymentReader.Setup(x => x.GetLatestByOrderIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, AdminOrderPaymentSummaryDto>());

        var sut = new DeliverDeliveryBatchCommandHandler(
            batchRepo.Object, orderRepo.Object, paymentReader.Object, Mock.Of<IOrdersUnitOfWork>());

        var result = await sut.Handle(
            new DeliverDeliveryBatchCommand(batch.Id, Guid.NewGuid()),
            CancellationToken.None);

        result.Status.Should().Be("Delivered");
        a.FulfillmentStatus.Should().Be(FulfillmentStatus.Delivered);
        b.FulfillmentStatus.Should().Be(FulfillmentStatus.Delivered);
    }

    private static CreateDeliveryBatchCommandHandler CreateCreateHandler(IReadOnlyList<Order> orders)
    {
        var orderRepo = new Mock<IOrderRepository>();
        orderRepo.Setup(x => x.GetByIdsWithItemsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(orders);

        var batchRepo = new Mock<IDeliveryBatchRepository>();
        batchRepo.Setup(x => x.GetOrderIdsInAnyBatchAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid>());
        batchRepo.Setup(x => x.AddAsync(It.IsAny<DeliveryBatch>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var numbers = new Mock<IDeliveryBatchNumberGenerator>();
        numbers.Setup(x => x.NextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(30001);

        var paymentReader = new Mock<IAdminOrderPixPaymentReader>();
        paymentReader.Setup(x => x.GetLatestByOrderIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, AdminOrderPaymentSummaryDto>());

        return new CreateDeliveryBatchCommandHandler(
            orderRepo.Object,
            batchRepo.Object,
            numbers.Object,
            paymentReader.Object,
            Mock.Of<IOrdersUnitOfWork>(),
            NullLogger<CreateDeliveryBatchCommandHandler>.Instance);
    }
}
