using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Vls.Shopflow.CartCheckout.Application.Interfaces;
using Vls.Shopflow.CartCheckout.Application.Repositories;
using Vls.Shopflow.CartCheckout.Domain.Entities;
using Vls.Shopflow.CartCheckout.Domain.Enums;
using Vls.Shopflow.Expiration.Application;
using Vls.Shopflow.Expiration.Application.Interfaces;
using Vls.Shopflow.Expiration.Application.Options;
using Vls.Shopflow.Orders.Application.Repositories;
using Vls.Shopflow.Orders.Domain.Entities;
using Vls.Shopflow.Orders.Domain.Enums;
using Vls.Shopflow.PaymentsPix.Application.Repositories;
using Vls.Shopflow.PaymentsPix.Domain.Entities;
using Vls.Shopflow.PaymentsPix.Domain.Enums;

namespace Vls.Shopflow.Expiration.UnitTests.Application;

public sealed class ExpirationProcessorTests
{
    private static CheckoutSession ExpiredPendingSession()
    {
        var reservationId = Guid.NewGuid();
        var item = CheckoutSessionItem.Create(
            Guid.NewGuid(),
            "Produto",
            "produto",
            Guid.NewGuid(),
            "SKU-1",
            1,
            10m,
            reservationId);

        return CheckoutSession.CreatePending(
            "Cliente",
            "cliente@test.com",
            "11999990000",
            "01001000",
            "Rua",
            "1",
            null,
            "Centro",
            "São Paulo",
            "SP",
            DateTimeOffset.UtcNow.AddMinutes(-5),
            new[] { item });
    }

    [Fact]
    public async Task ProcessAsync_ExpiresCheckoutSessionOrderAndPix()
    {
        var session = ExpiredPendingSession();
        var order = Order.CreatePendingPayment(
            session.Id,
            "Cliente",
            "cliente@test.com",
            "11999990000",
            "01001000",
            "Rua",
            "1",
            null,
            "Centro",
            "São Paulo",
            "SP",
            10m,
            null,
            10m,
            new[] { OrderItem.Create(Guid.NewGuid(), "Item", "SKU", 1, 10m) });

        var pix = PixPayment.CreatePending(
            order.Id,
            10m,
            PixPaymentProviderType.Fake,
            null,
            null,
            null,
            null,
            DateTimeOffset.UtcNow.AddMinutes(-1));

        var checkoutRepo = new Mock<ICheckoutSessionRepository>();
        checkoutRepo.Setup(x => x.GetExpiredPendingBatchAsync(
                It.IsAny<DateTimeOffset>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CheckoutSession> { session });
        checkoutRepo.Setup(x => x.GetByIdWithItemsAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var orderRepo = new Mock<IOrderRepository>();
        orderRepo.Setup(x => x.GetPendingPaymentByCheckoutSessionIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var pixRepo = new Mock<IPixPaymentRepository>();
        pixRepo.Setup(x => x.GetExpiredPendingBatchAsync(
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PixPayment>());
        pixRepo.Setup(x => x.GetPendingByOrderIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pix);

        var inventory = new Mock<IInventoryReservationService>();
        inventory.Setup(x => x.CancelReservationAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cartUow = new Mock<ICartCheckoutUnitOfWork>();
        cartUow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var ordersUow = new Mock<IOrdersUnitOfWork>();
        ordersUow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var pixUow = new Mock<IPaymentsPixUnitOfWork>();
        pixUow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var recovery = new Mock<IExpirationRecoveryReader>();
        recovery.Setup(x => x.GetOrphanPendingOrdersBatchAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<OrphanPendingOrderSnapshot>());

        var processor = new ExpirationProcessor(
            checkoutRepo.Object,
            orderRepo.Object,
            pixRepo.Object,
            inventory.Object,
            cartUow.Object,
            ordersUow.Object,
            pixUow.Object,
            recovery.Object,
            Options.Create(new ExpirationWorkerOptions { BatchSize = 50, PixPaymentTtlMinutes = 15 }),
            NullLogger<ExpirationProcessor>.Instance);

        var result = await processor.ProcessAsync(CancellationToken.None);

        result.ExpiredCheckoutSessions.Should().Be(1);
        result.ExpiredOrders.Should().Be(1);
        result.ExpiredPixPayments.Should().Be(1);
        result.CanceledReservations.Should().Be(1);
        session.Status.Should().Be(CheckoutSessionStatus.Expired);
        order.Status.Should().Be(OrderStatus.Expired);
        pix.Status.Should().Be(PixPaymentStatus.Expired);
    }

    [Fact]
    public async Task ProcessAsync_WhenReservationCancelFails_ContinuesAndExpiresSession()
    {
        var session = ExpiredPendingSession();

        var checkoutRepo = new Mock<ICheckoutSessionRepository>();
        checkoutRepo.Setup(x => x.GetExpiredPendingBatchAsync(
                It.IsAny<DateTimeOffset>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CheckoutSession> { session });

        var inventory = new Mock<IInventoryReservationService>();
        inventory.Setup(x => x.CancelReservationAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("inventory down"));

        var processor = CreateProcessor(
            checkoutRepo,
            inventory,
            orderRepo: null,
            pixRepo: null);

        var result = await processor.ProcessAsync(CancellationToken.None);

        result.Failures.Should().Be(1);
        session.Status.Should().Be(CheckoutSessionStatus.Pending);
    }

    private static ExpirationProcessor CreateProcessor(
        Mock<ICheckoutSessionRepository> checkoutRepo,
        Mock<IInventoryReservationService> inventory,
        Mock<IOrderRepository>? orderRepo,
        Mock<IPixPaymentRepository>? pixRepo)
    {
        orderRepo ??= new Mock<IOrderRepository>();
        orderRepo.Setup(x => x.GetPendingPaymentByCheckoutSessionIdAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        pixRepo ??= new Mock<IPixPaymentRepository>();
        pixRepo.Setup(x => x.GetExpiredPendingBatchAsync(
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PixPayment>());

        var recovery = new Mock<IExpirationRecoveryReader>();
        recovery.Setup(x => x.GetOrphanPendingOrdersBatchAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<OrphanPendingOrderSnapshot>());

        return new ExpirationProcessor(
            checkoutRepo.Object,
            orderRepo.Object,
            pixRepo.Object,
            inventory.Object,
            Mock.Of<ICartCheckoutUnitOfWork>(),
            Mock.Of<IOrdersUnitOfWork>(),
            Mock.Of<IPaymentsPixUnitOfWork>(),
            recovery.Object,
            Options.Create(new ExpirationWorkerOptions()),
            NullLogger<ExpirationProcessor>.Instance);
    }
}
