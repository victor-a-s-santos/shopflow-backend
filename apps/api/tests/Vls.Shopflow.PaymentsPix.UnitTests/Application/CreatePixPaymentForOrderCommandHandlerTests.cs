using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Vls.Shopflow.PaymentsPix.Application.CommandHandlers;
using Vls.Shopflow.PaymentsPix.Application.Commands;
using Vls.Shopflow.PaymentsPix.Application.Interfaces;
using Vls.Shopflow.PaymentsPix.Application.Repositories;
using Vls.Shopflow.PaymentsPix.Domain.Entities;
using Vls.Shopflow.PaymentsPix.Domain.Enums;
using Vls.Shopflow.PaymentsPix.Domain.Exceptions;

namespace Vls.Shopflow.PaymentsPix.UnitTests.Application;

public sealed class CreatePixPaymentForOrderCommandHandlerTests
{
    private static OrderPaymentSnapshot PendingOrder(Guid orderId, decimal total = 200m)
        => new(orderId, "PendingPayment", total, "João Silva", "joao@email.com");

    [Fact]
    public async Task Handle_WithValidOrder_CreatesPendingPixPayment()
    {
        var orderId = Guid.NewGuid();
        var orderReader = new Mock<IOrderPaymentReader>();
        orderReader.Setup(x => x.GetByIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PendingOrder(orderId));

        var provider = new Mock<IPixPaymentProvider>();
        provider.Setup(x => x.CreatePixChargeAsync(It.IsAny<PixChargeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PixChargeResponse(
                PixPaymentProviderType.Fake,
                "fake-dev-id",
                null,
                null,
                null,
                DateTimeOffset.UtcNow.AddMinutes(30),
                PixPaymentStatus.Pending));

        PixPayment? captured = null;
        var repository = new Mock<IPixPaymentRepository>();
        repository.Setup(x => x.GetPendingByOrderIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PixPayment?)null);
        repository.Setup(x => x.AddAsync(It.IsAny<PixPayment>(), It.IsAny<CancellationToken>()))
            .Callback<PixPayment, CancellationToken>((payment, _) => captured = payment)
            .Returns(Task.CompletedTask);

        var uow = new Mock<IPaymentsPixUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new CreatePixPaymentForOrderCommandHandler(
            orderReader.Object,
            repository.Object,
            provider.Object,
            uow.Object,
            NullLogger<CreatePixPaymentForOrderCommandHandler>.Instance);

        var result = await handler.Handle(
            new CreatePixPaymentForOrderCommand(orderId),
            CancellationToken.None);

        result.WasCreated.Should().BeTrue();
        result.Payment.OrderId.Should().Be(orderId);
        result.Payment.Status.Should().Be("Pending");
        result.Payment.Provider.Should().Be("Fake");
        result.Payment.Amount.Should().Be(200m);

        captured.Should().NotBeNull();
        captured!.Status.Should().Be(PixPaymentStatus.Pending);
    }

    [Fact]
    public async Task Handle_WhenOrderMissing_ThrowsNotFound()
    {
        var orderId = Guid.NewGuid();
        var orderReader = new Mock<IOrderPaymentReader>();
        orderReader.Setup(x => x.GetByIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrderPaymentSnapshot?)null);

        var repository = new Mock<IPixPaymentRepository>();
        repository.Setup(x => x.GetPendingByOrderIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PixPayment?)null);

        var handler = new CreatePixPaymentForOrderCommandHandler(
            orderReader.Object,
            repository.Object,
            Mock.Of<IPixPaymentProvider>(),
            Mock.Of<IPaymentsPixUnitOfWork>(),
            NullLogger<CreatePixPaymentForOrderCommandHandler>.Instance);

        var act = () => handler.Handle(
            new CreatePixPaymentForOrderCommand(orderId),
            CancellationToken.None);

        await act.Should().ThrowAsync<OrderNotFoundForPixPaymentException>();
    }

    [Fact]
    public async Task Handle_WhenOrderNotPendingPayment_ThrowsConflict()
    {
        var orderId = Guid.NewGuid();
        var orderReader = new Mock<IOrderPaymentReader>();
        orderReader.Setup(x => x.GetByIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PendingOrder(orderId) with { Status = "Paid" });

        var repository = new Mock<IPixPaymentRepository>();
        repository.Setup(x => x.GetPendingByOrderIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PixPayment?)null);

        var handler = new CreatePixPaymentForOrderCommandHandler(
            orderReader.Object,
            repository.Object,
            Mock.Of<IPixPaymentProvider>(),
            Mock.Of<IPaymentsPixUnitOfWork>(),
            NullLogger<CreatePixPaymentForOrderCommandHandler>.Instance);

        var act = () => handler.Handle(
            new CreatePixPaymentForOrderCommand(orderId),
            CancellationToken.None);

        await act.Should().ThrowAsync<OrderNotEligibleForPixPaymentException>();
    }

    [Fact]
    public async Task Handle_WhenPendingPaymentExists_ReturnsExisting()
    {
        var orderId = Guid.NewGuid();
        var existing = PixPayment.CreatePending(
            orderId,
            100m,
            PixPaymentProviderType.Fake,
            "fake-dev-id",
            null,
            null,
            null,
            DateTimeOffset.UtcNow.AddMinutes(30));

        var repository = new Mock<IPixPaymentRepository>();
        repository.Setup(x => x.GetPendingByOrderIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var handler = new CreatePixPaymentForOrderCommandHandler(
            Mock.Of<IOrderPaymentReader>(),
            repository.Object,
            Mock.Of<IPixPaymentProvider>(),
            Mock.Of<IPaymentsPixUnitOfWork>(),
            NullLogger<CreatePixPaymentForOrderCommandHandler>.Instance);

        var result = await handler.Handle(
            new CreatePixPaymentForOrderCommand(orderId),
            CancellationToken.None);

        result.WasCreated.Should().BeFalse();
        result.Payment.PaymentId.Should().Be(existing.Id);
        result.Payment.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task Handle_WhenOrderTotalInvalid_ThrowsBadRequest()
    {
        var orderId = Guid.NewGuid();
        var orderReader = new Mock<IOrderPaymentReader>();
        orderReader.Setup(x => x.GetByIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PendingOrder(orderId, 0m));

        var repository = new Mock<IPixPaymentRepository>();
        repository.Setup(x => x.GetPendingByOrderIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PixPayment?)null);

        var handler = new CreatePixPaymentForOrderCommandHandler(
            orderReader.Object,
            repository.Object,
            Mock.Of<IPixPaymentProvider>(),
            Mock.Of<IPaymentsPixUnitOfWork>(),
            NullLogger<CreatePixPaymentForOrderCommandHandler>.Instance);

        var act = () => handler.Handle(
            new CreatePixPaymentForOrderCommand(orderId),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOrderTotalForPixPaymentException>();
    }
}
