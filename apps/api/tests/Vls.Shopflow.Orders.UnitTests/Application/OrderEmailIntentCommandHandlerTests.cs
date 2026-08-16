using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
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

namespace Vls.Shopflow.Orders.UnitTests.Application;

public sealed class OrderEmailIntentCommandHandlerTests
{
    [Fact]
    public async Task CreateOrder_WhenSaveChangesFails_StillAttemptedIntentBeforeCommit()
    {
        var sessionId = Guid.NewGuid();
        OrderEmailIntent? captured = null;
        var intents = new Mock<IOrderEmailIntentRepository>();
        intents.Setup(x => x.EnsurePendingAsync(It.IsAny<OrderEmailIntent>(), It.IsAny<CancellationToken>()))
            .Callback<OrderEmailIntent, CancellationToken>((intent, _) => captured = intent)
            .Returns(Task.CompletedTask);

        var uow = new Mock<IOrdersUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        var handler = CreateOrderHandler(sessionId, intents.Object, uow.Object);
        var act = () => handler.Handle(new CreateOrderFromCheckoutSessionCommand(sessionId), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        captured.Should().NotBeNull();
        captured!.Type.Should().Be(OrderEmailIntentType.OrderCreated);
        captured.Status.Should().Be(OrderEmailIntentStatus.Pending);
        captured.IdempotencyKey.Should().Be(OrderEmailIntent.KeyFor(captured.OrderId, OrderEmailIntentType.OrderCreated));
        captured.PayloadJson.Should().Contain("joao@email.com");
        captured.PayloadJson.Should().Contain("raw-guest-token");
        captured.PayloadJson.Should().NotContain("<html");
    }

    [Fact]
    public async Task CreateOrder_OnCommit_CreatesExactlyOneCreatedIntent()
    {
        var sessionId = Guid.NewGuid();
        var captured = new List<OrderEmailIntent>();
        var intents = new Mock<IOrderEmailIntentRepository>();
        intents.Setup(x => x.EnsurePendingAsync(It.IsAny<OrderEmailIntent>(), It.IsAny<CancellationToken>()))
            .Callback<OrderEmailIntent, CancellationToken>((intent, _) => captured.Add(intent))
            .Returns(Task.CompletedTask);

        var uow = new Mock<IOrdersUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = CreateOrderHandler(sessionId, intents.Object, uow.Object);
        await handler.Handle(new CreateOrderFromCheckoutSessionCommand(sessionId), CancellationToken.None);

        captured.Should().ContainSingle();
        captured[0].Type.Should().Be(OrderEmailIntentType.OrderCreated);
        uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        intents.Verify(x => x.EnsurePendingAsync(It.IsAny<OrderEmailIntent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Ship_CreatesExactlyOneShippedIntent_BeforeSaveChanges()
    {
        var order = PaidOrder();
        OrderEmailIntent? captured = null;
        var intents = Capture(intent => captured = intent);
        var uow = new Mock<IOrdersUnitOfWork>();
        var sut = new ShipOrderFulfillmentCommandHandler(
            MockOrderRepo(order),
            Mock.Of<IAdminOrderPixPaymentReader>(),
            MockBatchRepo(),
            uow.Object,
            intents.Object);

        await sut.Handle(new ShipOrderFulfillmentCommand(order.Id, Guid.NewGuid(), "Carrier", "T1"), CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Type.Should().Be(OrderEmailIntentType.OrderShipped);
        captured.IdempotencyKey.Should().Be($"order:{order.Id:D}:shipped");
        uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Deliver_CreatesExactlyOneDeliveredIntent()
    {
        var order = PaidOrder();
        order.MarkAsShipped(Guid.NewGuid());
        OrderEmailIntent? captured = null;
        var intents = Capture(intent => captured = intent);
        var sut = new DeliverOrderFulfillmentCommandHandler(
            MockOrderRepo(order),
            Mock.Of<IAdminOrderPixPaymentReader>(),
            MockBatchRepo(),
            Mock.Of<IOrdersUnitOfWork>(),
            intents.Object);

        await sut.Handle(new DeliverOrderFulfillmentCommand(order.Id, Guid.NewGuid()), CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Type.Should().Be(OrderEmailIntentType.OrderDelivered);
        captured.IdempotencyKey.Should().Be($"order:{order.Id:D}:delivered");
    }

    [Fact]
    public async Task ShipBatch_CreatesOneIntentPerOrder()
    {
        var userId = Guid.NewGuid();
        var a = PaidOrder(userId, 1);
        var b = PaidOrder(userId, 2);
        var batch = DeliveryBatch.CreateAwaitingShipment(
            [a.Id, b.Id], userId, "Loja", "loja@test.com", "11999999999", false, Guid.NewGuid());
        batch.AssignBatchNumber(30050);

        var captured = new List<OrderEmailIntent>();
        var intents = Capture(intent => captured.Add(intent));
        var batchRepo = new Mock<IDeliveryBatchRepository>();
        batchRepo.Setup(x => x.GetByIdWithOrdersAsync(batch.Id, It.IsAny<CancellationToken>())).ReturnsAsync(batch);
        var orderRepo = new Mock<IOrderRepository>();
        orderRepo.Setup(x => x.GetByIdsWithItemsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([a, b]);

        var paymentReader = new Mock<IAdminOrderPixPaymentReader>();
        paymentReader.Setup(x => x.GetLatestByOrderIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, AdminOrderPaymentSummaryDto>());

        var sut = new ShipDeliveryBatchCommandHandler(
            batchRepo.Object,
            orderRepo.Object,
            paymentReader.Object,
            Mock.Of<IOrdersUnitOfWork>(),
            intents.Object);

        await sut.Handle(new ShipDeliveryBatchCommand(batch.Id, Guid.NewGuid(), "Correios", "BR1"), CancellationToken.None);

        captured.Should().HaveCount(2);
        captured.Select(i => i.OrderId).Should().BeEquivalentTo([a.Id, b.Id]);
        captured.Should().OnlyContain(i => i.Type == OrderEmailIntentType.OrderShipped);
        captured.Select(i => i.IdempotencyKey).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task DeliverBatch_CreatesOneIntentPerOrder()
    {
        var userId = Guid.NewGuid();
        var a = PaidOrder(userId, 1);
        var b = PaidOrder(userId, 2);
        a.MarkAsShipped(Guid.NewGuid());
        b.MarkAsShipped(Guid.NewGuid());
        var batch = DeliveryBatch.CreateAwaitingShipment(
            [a.Id, b.Id], userId, "Loja", "loja@test.com", "11999999999", false, Guid.NewGuid());
        batch.AssignBatchNumber(30051);
        batch.MarkAsShipped(Guid.NewGuid(), DeliveryMethod.Carrier, "T");

        var captured = new List<OrderEmailIntent>();
        var intents = Capture(intent => captured.Add(intent));
        var batchRepo = new Mock<IDeliveryBatchRepository>();
        batchRepo.Setup(x => x.GetByIdWithOrdersAsync(batch.Id, It.IsAny<CancellationToken>())).ReturnsAsync(batch);
        var orderRepo = new Mock<IOrderRepository>();
        orderRepo.Setup(x => x.GetByIdsWithItemsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([a, b]);
        var paymentReader = new Mock<IAdminOrderPixPaymentReader>();
        paymentReader.Setup(x => x.GetLatestByOrderIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, AdminOrderPaymentSummaryDto>());

        var sut = new DeliverDeliveryBatchCommandHandler(
            batchRepo.Object,
            orderRepo.Object,
            paymentReader.Object,
            Mock.Of<IOrdersUnitOfWork>(),
            intents.Object);

        await sut.Handle(new DeliverDeliveryBatchCommand(batch.Id, Guid.NewGuid()), CancellationToken.None);

        captured.Should().HaveCount(2);
        captured.Should().OnlyContain(i => i.Type == OrderEmailIntentType.OrderDelivered);
        captured.Select(i => i.OrderId).Should().BeEquivalentTo([a.Id, b.Id]);
    }

    private static CreateOrderFromCheckoutSessionCommandHandler CreateOrderHandler(
        Guid sessionId,
        IOrderEmailIntentRepository intents,
        IOrdersUnitOfWork uow)
    {
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

        var reader = new Mock<ICheckoutSessionReader>();
        reader.Setup(x => x.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(snapshot);
        var repo = new Mock<IOrderRepository>();
        repo.Setup(x => x.GetByCheckoutSessionIdWithItemsAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);
        repo.Setup(x => x.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var numbers = new Mock<IOrderNumberGenerator>();
        numbers.Setup(x => x.NextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(10582);
        var hasher = new Mock<IGuestOrderAccessTokenHasher>();
        hasher.Setup(x => x.GenerateRawToken()).Returns("raw-guest-token");
        hasher.Setup(x => x.Hash("raw-guest-token")).Returns("hashed-guest-token");

        return new CreateOrderFromCheckoutSessionCommandHandler(
            reader.Object,
            repo.Object,
            numbers.Object,
            Mock.Of<IGuestOrderAccessTokenRepository>(),
            hasher.Object,
            uow,
            Options.Create(new GuestOrderAccessOptions
            {
                Enabled = true,
                TokenTtlDays = 30,
                TokenHashSecret = "test-secret"
            }),
            intents,
            NullLogger<CreateOrderFromCheckoutSessionCommandHandler>.Instance);
    }

    private static Mock<IOrderEmailIntentRepository> Capture(Action<OrderEmailIntent> onAdd)
    {
        var intents = new Mock<IOrderEmailIntentRepository>();
        intents.Setup(x => x.EnsurePendingAsync(It.IsAny<OrderEmailIntent>(), It.IsAny<CancellationToken>()))
            .Callback<OrderEmailIntent, CancellationToken>((intent, _) => onAdd(intent))
            .Returns(Task.CompletedTask);
        return intents;
    }

    private static IOrderRepository MockOrderRepo(Order order)
    {
        var repo = new Mock<IOrderRepository>();
        repo.Setup(x => x.GetByIdWithItemsAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        return repo.Object;
    }

    private static IDeliveryBatchRepository MockBatchRepo()
    {
        var mock = new Mock<IDeliveryBatchRepository>();
        mock.Setup(x => x.FindMembershipByOrderIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DeliveryBatchMembership?)null);
        return mock.Object;
    }

    private static Order PaidOrder(Guid? customerUserId = null, long orderNumber = 30001)
    {
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
            [OrderItem.Create(Guid.NewGuid(), "Produto", "SKU-1", 1, 50m)],
            customerUserId);
        order.AssignOrderNumber(orderNumber);
        order.MarkAsPaid();
        return order;
    }
}
