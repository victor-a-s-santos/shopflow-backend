using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Vls.Shopflow.PaymentsPix.Application.CommandHandlers;
using Vls.Shopflow.PaymentsPix.Application.Commands;
using Vls.Shopflow.PaymentsPix.Application.Interfaces;
using Vls.Shopflow.PaymentsPix.Application.Options;
using Vls.Shopflow.PaymentsPix.Application.Repositories;
using Vls.Shopflow.PaymentsPix.Domain.Entities;
using Vls.Shopflow.PaymentsPix.Domain.Enums;

namespace Vls.Shopflow.PaymentsPix.UnitTests.Application;

public sealed class ProcessMercadoPagoPixWebhookCommandHandlerTests
{
    private static PixPayment CreatePendingPayment(
        Guid orderId,
        decimal amount,
        string providerOrderId,
        string? copyPaste = null)
        => PixPayment.CreatePending(
            orderId,
            amount,
            PixPaymentProviderType.MercadoPago,
            providerOrderId,
            $"PAY-{providerOrderId}",
            null,
            null,
            copyPaste,
            null,
            "action_required",
            "waiting_transfer",
            "action_required",
            "waiting_transfer",
            orderId.ToString("D"),
            orderId.ToString("D"),
            DateTimeOffset.UtcNow.AddMinutes(30));

    private static MercadoPagoOrderLookup OrderLookup(
        string orderId,
        string status,
        string? statusDetail,
        Guid shopflowOrderId,
        decimal amount,
        string? transactionStatus = null,
        string? transactionStatusDetail = null)
        => new(
            orderId,
            status,
            statusDetail,
            shopflowOrderId.ToString("D"),
            amount,
            $"PAY-{orderId}",
            amount,
            transactionStatus ?? status,
            transactionStatusDetail ?? statusDetail,
            "pix",
            "bank_transfer",
            null,
            null,
            null,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

    [Fact]
    public async Task Handle_InvalidSignature_Returns401()
    {
        var signature = new Mock<IMercadoPagoWebhookSignatureValidator>();
        signature.Setup(x => x.IsValid(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>(), out It.Ref<string?>.IsAny))
            .Returns(false);

        var handler = CreateHandler(signatureValidator: signature.Object);

        var result = await handler.Handle(
            new ProcessMercadoPagoPixWebhookCommand("ORD1", null, "sig", "req", "order.updated", "order", false, "1"),
            CancellationToken.None);

        result.StatusCode.Should().Be(401);
        result.Outcome.Should().Be("InvalidSignature");
    }

    [Fact]
    public async Task Handle_ProcessedAccredited_MarksPaidAndConfirmsReservation()
    {
        var orderId = Guid.NewGuid();
        var checkoutSessionId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        const string providerOrderId = "ORD01TESTPAID";
        var payment = CreatePendingPayment(orderId, 59.90m, providerOrderId, "qr");

        var paymentRepo = new Mock<IPixPaymentRepository>();
        paymentRepo.Setup(x => x.GetByProviderOrderIdAsync(providerOrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        var orderClient = new Mock<IMercadoPagoOrderClient>();
        orderClient.Setup(x => x.GetOrderAsync(providerOrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OrderLookup(
                providerOrderId,
                "processed",
                "accredited",
                orderId,
                59.90m,
                "processed",
                "accredited"));

        var orderWriter = new Mock<IOrderPaidWriter>();
        orderWriter.Setup(x => x.GetAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderPaidWriteResult(true, false, false, "PendingPayment", checkoutSessionId));
        orderWriter.Setup(x => x.MarkAsPaidAsync(orderId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderPaidWriteResult(true, false, true, "Paid", checkoutSessionId));

        var reservationReader = new Mock<ICheckoutReservationIdsReader>();
        reservationReader.Setup(x => x.GetReservationIdsByCheckoutSessionAsync(checkoutSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([reservationId]);

        var confirmer = new Mock<IInventoryReservationConfirmer>();
        var webhookEvents = new Mock<IMercadoPagoWebhookEventRepository>();
        var uow = new Mock<IPaymentsPixUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = CreateHandler(
            signatureValidator: ValidSignature().Object,
            orderClient: orderClient.Object,
            paymentRepository: paymentRepo.Object,
            webhookEventRepository: webhookEvents.Object,
            orderPaidWriter: orderWriter.Object,
            reservationIdsReader: reservationReader.Object,
            reservationConfirmer: confirmer.Object,
            unitOfWork: uow.Object);

        var result = await handler.Handle(
            new ProcessMercadoPagoPixWebhookCommand(providerOrderId, null, "sig", "req", "order.updated", "order", false, "evt-1"),
            CancellationToken.None);

        result.StatusCode.Should().Be(200);
        result.Outcome.Should().Be("Paid");
        payment.Status.Should().Be(PixPaymentStatus.Paid);
        confirmer.Verify(x => x.ConfirmAsync(reservationId, It.IsAny<CancellationToken>()), Times.Once);
        orderWriter.Verify(x => x.MarkAsPaidAsync(orderId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Pending_DoesNotMarkPaid()
    {
        var orderId = Guid.NewGuid();
        const string providerOrderId = "ORD01PENDING";
        var payment = CreatePendingPayment(orderId, 10m, providerOrderId);

        var paymentRepo = new Mock<IPixPaymentRepository>();
        paymentRepo.Setup(x => x.GetByProviderOrderIdAsync(providerOrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        var orderClient = new Mock<IMercadoPagoOrderClient>();
        orderClient.Setup(x => x.GetOrderAsync(providerOrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OrderLookup(providerOrderId, "action_required", "waiting_transfer", orderId, 10m));

        var orderWriter = new Mock<IOrderPaidWriter>();
        var confirmer = new Mock<IInventoryReservationConfirmer>();

        var handler = CreateHandler(
            signatureValidator: ValidSignature().Object,
            orderClient: orderClient.Object,
            paymentRepository: paymentRepo.Object,
            orderPaidWriter: orderWriter.Object,
            reservationConfirmer: confirmer.Object);

        var result = await handler.Handle(
            new ProcessMercadoPagoPixWebhookCommand(providerOrderId, null, "sig", "req", "order.updated", "order", false, null),
            CancellationToken.None);

        result.Outcome.Should().Be("Pending");
        payment.Status.Should().Be(PixPaymentStatus.Pending);
        orderWriter.Verify(x => x.MarkAsPaidAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
        confirmer.Verify(x => x.ConfirmAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Failed_DoesNotConfirmInventory()
    {
        var orderId = Guid.NewGuid();
        const string providerOrderId = "ORD01FAILED";
        var payment = CreatePendingPayment(orderId, 10m, providerOrderId);

        var paymentRepo = new Mock<IPixPaymentRepository>();
        paymentRepo.Setup(x => x.GetByProviderOrderIdAsync(providerOrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        var orderClient = new Mock<IMercadoPagoOrderClient>();
        orderClient.Setup(x => x.GetOrderAsync(providerOrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OrderLookup(providerOrderId, "failed", "failed", orderId, 10m));

        var confirmer = new Mock<IInventoryReservationConfirmer>();

        var handler = CreateHandler(
            signatureValidator: ValidSignature().Object,
            orderClient: orderClient.Object,
            paymentRepository: paymentRepo.Object,
            reservationConfirmer: confirmer.Object);

        var result = await handler.Handle(
            new ProcessMercadoPagoPixWebhookCommand(providerOrderId, null, "sig", "req", "order.updated", "order", false, null),
            CancellationToken.None);

        result.Outcome.Should().Be("Failed");
        payment.Status.Should().Be(PixPaymentStatus.Failed);
        confirmer.Verify(x => x.ConfirmAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_AmountMismatch_DoesNotMarkPaid()
    {
        var orderId = Guid.NewGuid();
        const string providerOrderId = "ORD01MISMATCH";
        var payment = CreatePendingPayment(orderId, 10m, providerOrderId);

        var paymentRepo = new Mock<IPixPaymentRepository>();
        paymentRepo.Setup(x => x.GetByProviderOrderIdAsync(providerOrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        var orderClient = new Mock<IMercadoPagoOrderClient>();
        orderClient.Setup(x => x.GetOrderAsync(providerOrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OrderLookup(
                providerOrderId,
                "processed",
                "accredited",
                orderId,
                99m,
                "processed",
                "accredited"));

        var orderWriter = new Mock<IOrderPaidWriter>();

        var handler = CreateHandler(
            signatureValidator: ValidSignature().Object,
            orderClient: orderClient.Object,
            paymentRepository: paymentRepo.Object,
            orderPaidWriter: orderWriter.Object);

        var result = await handler.Handle(
            new ProcessMercadoPagoPixWebhookCommand(providerOrderId, null, "sig", "req", "order.updated", "order", false, null),
            CancellationToken.None);

        result.Outcome.Should().Be("AmountMismatch");
        payment.Status.Should().Be(PixPaymentStatus.Pending);
        orderWriter.Verify(x => x.MarkAsPaidAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_PaymentType_IsIgnored()
    {
        var handler = CreateHandler(signatureValidator: ValidSignature().Object);

        var result = await handler.Handle(
            new ProcessMercadoPagoPixWebhookCommand("ORD1", null, "sig", "req", "payment.updated", "payment", false, null),
            CancellationToken.None);

        result.Outcome.Should().Be("IgnoredType");
    }

    private static Mock<IMercadoPagoWebhookSignatureValidator> ValidSignature()
    {
        var signature = new Mock<IMercadoPagoWebhookSignatureValidator>();
        string? reason = null;
        signature.Setup(x => x.IsValid(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>(), out reason))
            .Returns(true);
        return signature;
    }

    private static ProcessMercadoPagoPixWebhookCommandHandler CreateHandler(
        IMercadoPagoWebhookSignatureValidator? signatureValidator = null,
        IMercadoPagoOrderClient? orderClient = null,
        IPixPaymentRepository? paymentRepository = null,
        IMercadoPagoWebhookEventRepository? webhookEventRepository = null,
        IOrderPaidWriter? orderPaidWriter = null,
        ICheckoutReservationIdsReader? reservationIdsReader = null,
        IInventoryReservationConfirmer? reservationConfirmer = null,
        IPaymentsPixUnitOfWork? unitOfWork = null)
    {
        var webhookEvents = webhookEventRepository ?? Mock.Of<IMercadoPagoWebhookEventRepository>();
        var uow = unitOfWork ?? Mock.Of<IPaymentsPixUnitOfWork>(x =>
            x.SaveChangesAsync(It.IsAny<CancellationToken>()) == Task.FromResult(1));

        return new ProcessMercadoPagoPixWebhookCommandHandler(
            Options.Create(new MercadoPagoOptions { WebhookSecret = "secret" }),
            signatureValidator ?? ValidSignature().Object,
            orderClient ?? Mock.Of<IMercadoPagoOrderClient>(),
            paymentRepository ?? Mock.Of<IPixPaymentRepository>(),
            webhookEvents,
            orderPaidWriter ?? Mock.Of<IOrderPaidWriter>(),
            reservationIdsReader ?? Mock.Of<ICheckoutReservationIdsReader>(),
            reservationConfirmer ?? Mock.Of<IInventoryReservationConfirmer>(),
            uow,
            NullLogger<ProcessMercadoPagoPixWebhookCommandHandler>.Instance);
    }
}
