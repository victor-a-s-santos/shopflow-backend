using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Vls.Shopflow.PaymentsPix.Application.CommandHandlers;
using Vls.Shopflow.PaymentsPix.Application.Commands;
using Vls.Shopflow.PaymentsPix.Application.Interfaces;
using Vls.Shopflow.PaymentsPix.Application.Options;
using Vls.Shopflow.PaymentsPix.Application.Repositories;
using Vls.Shopflow.PaymentsPix.Application.Services;
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

    private static MercadoPagoOrderLookupResult FoundLookup(MercadoPagoOrderLookup order)
        => new(MercadoPagoOrderLookupStatus.Found, order, 200, null);

    [Fact]
    public async Task Handle_InvalidSignature_Returns401()
    {
        var signature = new Mock<IMercadoPagoWebhookSignatureValidator>();
        var diagnostics = new MercadoPagoWebhookSignatureDiagnostics(
            true, true, true, false, true, true, true, 0, true, "deadbeef", "cafebabe",
            "id/request-id/ts", "ORD***", "req***", "signature_mismatch");
        signature.Setup(x => x.Validate(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(new MercadoPagoWebhookSignatureValidationResult(
                false, "Signature mismatch.", "signature_mismatch", diagnostics));

        var rawCapture = new Mock<IMercadoPagoWebhookRawCapture>();
        var handler = CreateHandler(signatureValidator: signature.Object, webhookRawCapture: rawCapture.Object);

        var result = await handler.Handle(
            new ProcessMercadoPagoPixWebhookCommand("ORD1", null, "sig", "req", "order.updated", "order", false, "1"),
            CancellationToken.None);

        result.StatusCode.Should().Be(401);
        result.Outcome.Should().Be("InvalidSignature");
        rawCapture.Verify(
            x => x.TryCapture(
                It.IsAny<MercadoPagoWebhookRawCaptureInput>(),
                It.Is<MercadoPagoWebhookSignatureValidationResult>(r => !r.IsValid)),
            Times.Once);
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
            .ReturnsAsync(FoundLookup(OrderLookup(
                providerOrderId,
                "processed",
                "accredited",
                orderId,
                59.90m,
                "processed",
                "accredited")));

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
            .ReturnsAsync(FoundLookup(OrderLookup(providerOrderId, "action_required", "waiting_transfer", orderId, 10m)));

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
            .ReturnsAsync(FoundLookup(OrderLookup(providerOrderId, "failed", "failed", orderId, 10m)));

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
        payment.FailureReason.Should().Be("Provider order failed.");
        payment.ProviderStatusDetail.Should().Be("failed");
        confirmer.Verify(x => x.ConfirmAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Canceled_SetsHumanReadableFailureReason()
    {
        var orderId = Guid.NewGuid();
        const string providerOrderId = "ORD01CANCELED";
        var payment = CreatePendingPayment(orderId, 10m, providerOrderId);

        var paymentRepo = new Mock<IPixPaymentRepository>();
        paymentRepo.Setup(x => x.GetByProviderOrderIdAsync(providerOrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        var orderClient = new Mock<IMercadoPagoOrderClient>();
        orderClient.Setup(x => x.GetOrderAsync(providerOrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FoundLookup(OrderLookup(providerOrderId, "canceled", "by_collector", orderId, 10m)));

        var handler = CreateHandler(
            signatureValidator: ValidSignature().Object,
            orderClient: orderClient.Object,
            paymentRepository: paymentRepo.Object);

        var result = await handler.Handle(
            new ProcessMercadoPagoPixWebhookCommand(providerOrderId, null, "sig", "req", "order.updated", "order", false, null),
            CancellationToken.None);

        result.Outcome.Should().Be("Canceled");
        payment.Status.Should().Be(PixPaymentStatus.Canceled);
        payment.FailureReason.Should().Be("Provider order canceled.");
        payment.ProviderStatusDetail.Should().Be("by_collector");
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
            .ReturnsAsync(FoundLookup(OrderLookup(
                providerOrderId,
                "processed",
                "accredited",
                orderId,
                99m,
                "processed",
                "accredited")));

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

    [Fact]
    public async Task Handle_DuplicateProviderEventId_AlreadyProcessed_DoesNotInsert()
    {
        const string providerEventId = "evt-already-processed";
        var existing = MercadoPagoWebhookEvent.CreateReceived(
            "ORD1", providerEventId, "req", "order.updated", "order", false, true);
        existing.MarkProcessed();

        var webhookEvents = new Mock<IMercadoPagoWebhookEventRepository>();
        webhookEvents.Setup(x => x.GetByProviderEventIdAsync(providerEventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var handler = CreateHandler(
            signatureValidator: ValidSignature().Object,
            webhookEventRepository: webhookEvents.Object);

        var result = await handler.Handle(
            new ProcessMercadoPagoPixWebhookCommand("ORD1", null, "sig", "req", "order.updated", "order", false, providerEventId),
            CancellationToken.None);

        result.StatusCode.Should().Be(200);
        result.Outcome.Should().Be("AlreadyProcessed");
        webhookEvents.Verify(
            x => x.AddAsync(It.IsAny<MercadoPagoWebhookEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_DuplicateProviderEventId_AlreadyIgnored_DoesNotInsert()
    {
        const string providerEventId = "evt-already-ignored";
        var existing = MercadoPagoWebhookEvent.CreateReceived(
            "ORD1", providerEventId, "req", "payment.updated", "payment", false, true);
        existing.MarkIgnored("Webhook type 'payment' ignored; expected order.");

        var webhookEvents = new Mock<IMercadoPagoWebhookEventRepository>();
        webhookEvents.Setup(x => x.GetByProviderEventIdAsync(providerEventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var handler = CreateHandler(
            signatureValidator: ValidSignature().Object,
            webhookEventRepository: webhookEvents.Object);

        var result = await handler.Handle(
            new ProcessMercadoPagoPixWebhookCommand("ORD1", null, "sig", "req", "order.updated", "order", false, providerEventId),
            CancellationToken.None);

        result.StatusCode.Should().Be(200);
        result.Outcome.Should().Be("AlreadyIgnored");
        webhookEvents.Verify(
            x => x.AddAsync(It.IsAny<MercadoPagoWebhookEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("Failed")]
    [InlineData("Received")]
    public async Task Handle_DuplicateProviderEventId_FailedOrReceived_ReusesRowWithoutInsert(string priorStatus)
    {
        var orderId = Guid.NewGuid();
        const string providerOrderId = "ORD01RETRY";
        const string providerEventId = "evt-retry";
        var payment = CreatePendingPayment(orderId, 10m, providerOrderId);

        var existing = MercadoPagoWebhookEvent.CreateReceived(
            providerOrderId, providerEventId, "req", "order.updated", "order", false, true);
        if (priorStatus == "Failed")
            existing.MarkFailed("previous failure");

        existing.ProcessingStatus.Should().Be(priorStatus);

        var webhookEvents = new Mock<IMercadoPagoWebhookEventRepository>();
        webhookEvents.Setup(x => x.GetByProviderEventIdAsync(providerEventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var paymentRepo = new Mock<IPixPaymentRepository>();
        paymentRepo.Setup(x => x.GetByProviderOrderIdAsync(providerOrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        var orderClient = new Mock<IMercadoPagoOrderClient>();
        orderClient.Setup(x => x.GetOrderAsync(providerOrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FoundLookup(OrderLookup(providerOrderId, "action_required", "waiting_transfer", orderId, 10m)));

        var handler = CreateHandler(
            signatureValidator: ValidSignature().Object,
            orderClient: orderClient.Object,
            paymentRepository: paymentRepo.Object,
            webhookEventRepository: webhookEvents.Object);

        var result = await handler.Handle(
            new ProcessMercadoPagoPixWebhookCommand(
                providerOrderId, null, "sig", "req", "order.updated", "order", false, providerEventId),
            CancellationToken.None);

        result.Outcome.Should().Be("Pending");
        existing.ProcessingStatus.Should().Be("Processed");
        webhookEvents.Verify(
            x => x.AddAsync(It.IsAny<MercadoPagoWebhookEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_OrderMarkPaidFails_LeavesPixPaymentPending()
    {
        var orderId = Guid.NewGuid();
        var checkoutSessionId = Guid.NewGuid();
        const string providerOrderId = "ORD01ORDERFAIL";
        var payment = CreatePendingPayment(orderId, 59.90m, providerOrderId, "qr");

        var paymentRepo = new Mock<IPixPaymentRepository>();
        paymentRepo.Setup(x => x.GetByProviderOrderIdAsync(providerOrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        var orderClient = new Mock<IMercadoPagoOrderClient>();
        orderClient.Setup(x => x.GetOrderAsync(providerOrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FoundLookup(OrderLookup(
                providerOrderId,
                "processed",
                "accredited",
                orderId,
                59.90m,
                "processed",
                "accredited")));

        var orderWriter = new Mock<IOrderPaidWriter>();
        orderWriter.Setup(x => x.GetAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderPaidWriteResult(true, false, false, "PendingPayment", checkoutSessionId));
        orderWriter.Setup(x => x.MarkAsPaidAsync(orderId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderPaidWriteResult(true, false, false, "Cancelled", checkoutSessionId));

        var reservationReader = new Mock<ICheckoutReservationIdsReader>();
        reservationReader.Setup(x => x.GetReservationIdsByCheckoutSessionAsync(checkoutSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var handler = CreateHandler(
            signatureValidator: ValidSignature().Object,
            orderClient: orderClient.Object,
            paymentRepository: paymentRepo.Object,
            orderPaidWriter: orderWriter.Object,
            reservationIdsReader: reservationReader.Object);

        var result = await handler.Handle(
            new ProcessMercadoPagoPixWebhookCommand(providerOrderId, null, "sig", "req", "order.updated", "order", false, "evt-fail"),
            CancellationToken.None);

        result.Outcome.Should().Be("OrderMarkPaidFailed");
        payment.Status.Should().Be(PixPaymentStatus.Pending);
        orderWriter.Verify(x => x.MarkAsPaidAsync(orderId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_PanelSimulationDataId_123456_IgnoresWithoutCallingLookupOrMarkingPaid()
    {
        const string fakeId = "123456";
        MercadoPagoWebhookEvent? captured = null;

        var orderClient = new Mock<IMercadoPagoOrderClient>();
        var orderWriter = new Mock<IOrderPaidWriter>();
        var confirmer = new Mock<IInventoryReservationConfirmer>();
        var webhookEvents = new Mock<IMercadoPagoWebhookEventRepository>();
        webhookEvents
            .Setup(x => x.AddAsync(It.IsAny<MercadoPagoWebhookEvent>(), It.IsAny<CancellationToken>()))
            .Callback<MercadoPagoWebhookEvent, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);

        var handler = CreateHandler(
            signatureValidator: ValidSignature().Object,
            orderClient: orderClient.Object,
            webhookEventRepository: webhookEvents.Object,
            orderPaidWriter: orderWriter.Object,
            reservationConfirmer: confirmer.Object);

        var result = await handler.Handle(
            new ProcessMercadoPagoPixWebhookCommand(
                fakeId, fakeId, "sig", "req", "order.updated", "order", false, "evt-sim-123456"),
            CancellationToken.None);

        result.StatusCode.Should().Be(200);
        result.Outcome.Should().Be("SimulatorEvent");
        captured.Should().NotBeNull();
        captured!.ProcessingStatus.Should().Be("Ignored");
        captured.ErrorMessage.Should().Contain("SimulatorEvent");
        orderClient.Verify(x => x.GetOrderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        orderWriter.Verify(x => x.MarkAsPaidAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
        confirmer.Verify(x => x.ConfirmAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_OrdTstId_CallsGetOrderAsync()
    {
        var orderId = Guid.NewGuid();
        const string providerOrderId = "ORDTST01SANDBOXEXAMPLE";
        var payment = CreatePendingPayment(orderId, 10m, providerOrderId);

        var paymentRepo = new Mock<IPixPaymentRepository>();
        paymentRepo.Setup(x => x.GetByProviderOrderIdAsync(providerOrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        var orderClient = new Mock<IMercadoPagoOrderClient>();
        orderClient.Setup(x => x.GetOrderAsync(providerOrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FoundLookup(OrderLookup(providerOrderId, "action_required", "waiting_transfer", orderId, 10m)));

        var handler = CreateHandler(
            signatureValidator: ValidSignature().Object,
            orderClient: orderClient.Object,
            paymentRepository: paymentRepo.Object);

        var result = await handler.Handle(
            new ProcessMercadoPagoPixWebhookCommand(providerOrderId, null, "sig", "req", "order.updated", "order", false, null),
            CancellationToken.None);

        result.Outcome.Should().Be("Pending");
        orderClient.Verify(x => x.GetOrderAsync(providerOrderId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_LookupBadRequest_Returns200LookupFailed_DoesNotMarkPaid()
    {
        const string providerOrderId = "ORD01BADREQUEST";
        MercadoPagoWebhookEvent? captured = null;

        var orderClient = new Mock<IMercadoPagoOrderClient>();
        orderClient.Setup(x => x.GetOrderAsync(providerOrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MercadoPagoOrderLookupResult(
                MercadoPagoOrderLookupStatus.BadRequest,
                null,
                400,
                "Mercado Pago rejected order id (bad request)."));

        var orderWriter = new Mock<IOrderPaidWriter>();
        var webhookEvents = new Mock<IMercadoPagoWebhookEventRepository>();
        webhookEvents
            .Setup(x => x.AddAsync(It.IsAny<MercadoPagoWebhookEvent>(), It.IsAny<CancellationToken>()))
            .Callback<MercadoPagoWebhookEvent, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);

        var handler = CreateHandler(
            signatureValidator: ValidSignature().Object,
            orderClient: orderClient.Object,
            webhookEventRepository: webhookEvents.Object,
            orderPaidWriter: orderWriter.Object);

        var result = await handler.Handle(
            new ProcessMercadoPagoPixWebhookCommand(providerOrderId, null, "sig", "req", "order.updated", "order", false, "evt-400"),
            CancellationToken.None);

        result.StatusCode.Should().Be(200);
        result.Outcome.Should().Be("LookupFailed");
        captured!.ProcessingStatus.Should().Be("LookupFailed");
        orderWriter.Verify(x => x.MarkAsPaidAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_LookupNotFound_Returns200LookupFailed_DoesNotMarkPaid()
    {
        const string providerOrderId = "ORD01MISSING";
        MercadoPagoWebhookEvent? captured = null;

        var orderClient = new Mock<IMercadoPagoOrderClient>();
        orderClient.Setup(x => x.GetOrderAsync(providerOrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MercadoPagoOrderLookupResult(
                MercadoPagoOrderLookupStatus.NotFound,
                null,
                404,
                "Order not found at Mercado Pago."));

        var orderWriter = new Mock<IOrderPaidWriter>();
        var webhookEvents = new Mock<IMercadoPagoWebhookEventRepository>();
        webhookEvents
            .Setup(x => x.AddAsync(It.IsAny<MercadoPagoWebhookEvent>(), It.IsAny<CancellationToken>()))
            .Callback<MercadoPagoWebhookEvent, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);

        var handler = CreateHandler(
            signatureValidator: ValidSignature().Object,
            orderClient: orderClient.Object,
            webhookEventRepository: webhookEvents.Object,
            orderPaidWriter: orderWriter.Object);

        var result = await handler.Handle(
            new ProcessMercadoPagoPixWebhookCommand(providerOrderId, null, "sig", "req", "order.updated", "order", false, "evt-404"),
            CancellationToken.None);

        result.StatusCode.Should().Be(200);
        result.Outcome.Should().Be("LookupFailed");
        captured!.ProcessingStatus.Should().Be("LookupFailed");
        orderWriter.Verify(x => x.MarkAsPaidAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DuplicateProviderEventId_AlreadyLookupFailed_DoesNotInsert()
    {
        const string providerEventId = "evt-already-lookup-failed";
        var existing = MercadoPagoWebhookEvent.CreateReceived(
            "ORD1", providerEventId, "req", "order.updated", "order", false, true);
        existing.MarkLookupFailed("Order not found at Mercado Pago.");

        var webhookEvents = new Mock<IMercadoPagoWebhookEventRepository>();
        webhookEvents.Setup(x => x.GetByProviderEventIdAsync(providerEventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var orderClient = new Mock<IMercadoPagoOrderClient>();

        var handler = CreateHandler(
            signatureValidator: ValidSignature().Object,
            orderClient: orderClient.Object,
            webhookEventRepository: webhookEvents.Object);

        var result = await handler.Handle(
            new ProcessMercadoPagoPixWebhookCommand("ORD1", null, "sig", "req", "order.updated", "order", false, providerEventId),
            CancellationToken.None);

        result.StatusCode.Should().Be(200);
        result.Outcome.Should().Be("AlreadyLookupFailed");
        orderClient.Verify(x => x.GetOrderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        webhookEvents.Verify(
            x => x.AddAsync(It.IsAny<MercadoPagoWebhookEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_QueryBodyDataIdMismatch_IgnoresWithoutMarkingPaid()
    {
        const string queryId = "ORD01QUERY";
        const string bodyId = "ORD01BODYDIFF";
        MercadoPagoWebhookEvent? captured = null;

        var orderClient = new Mock<IMercadoPagoOrderClient>();
        var orderWriter = new Mock<IOrderPaidWriter>();
        var webhookEvents = new Mock<IMercadoPagoWebhookEventRepository>();
        webhookEvents
            .Setup(x => x.AddAsync(It.IsAny<MercadoPagoWebhookEvent>(), It.IsAny<CancellationToken>()))
            .Callback<MercadoPagoWebhookEvent, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);

        var handler = CreateHandler(
            signatureValidator: ValidSignature().Object,
            orderClient: orderClient.Object,
            webhookEventRepository: webhookEvents.Object,
            orderPaidWriter: orderWriter.Object);

        var result = await handler.Handle(
            new ProcessMercadoPagoPixWebhookCommand(
                queryId, bodyId, "sig", "req", "order.updated", "order", false, "evt-mismatch"),
            CancellationToken.None);

        result.StatusCode.Should().Be(200);
        result.Outcome.Should().Be("DataIdMismatch");
        captured!.ProcessingStatus.Should().Be("Ignored");
        orderClient.Verify(x => x.GetOrderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        orderWriter.Verify(x => x.MarkAsPaidAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_MissingQueryDataId_IgnoresWithoutMarkingPaid()
    {
        var orderClient = new Mock<IMercadoPagoOrderClient>();
        var orderWriter = new Mock<IOrderPaidWriter>();
        var webhookEvents = new Mock<IMercadoPagoWebhookEventRepository>();
        MercadoPagoWebhookEvent? captured = null;
        webhookEvents
            .Setup(x => x.AddAsync(It.IsAny<MercadoPagoWebhookEvent>(), It.IsAny<CancellationToken>()))
            .Callback<MercadoPagoWebhookEvent, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);

        var handler = CreateHandler(
            signatureValidator: ValidSignature().Object,
            orderClient: orderClient.Object,
            webhookEventRepository: webhookEvents.Object,
            orderPaidWriter: orderWriter.Object);

        var result = await handler.Handle(
            new ProcessMercadoPagoPixWebhookCommand(
                null, "ORD01BODYONLY", "sig", "req", "order.updated", "order", false, "evt-no-query"),
            CancellationToken.None);

        result.StatusCode.Should().Be(200);
        result.Outcome.Should().Be("MissingQueryDataId");
        captured!.ProcessingStatus.Should().Be("Ignored");
        orderClient.Verify(x => x.GetOrderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        orderWriter.Verify(x => x.MarkAsPaidAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_InvalidSignature_WithBodyApplicationIds_StillReturns401()
    {
        var signature = new Mock<IMercadoPagoWebhookSignatureValidator>();
        var diagnostics = new MercadoPagoWebhookSignatureDiagnostics(
            true, true, true, true, true, true, true, 12, true, "aabbccdd", "11223344",
            "id/request-id/ts", "ORDTS…9S2", "2066ca…dd06", "signature_mismatch");
        signature.Setup(x => x.Validate(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(new MercadoPagoWebhookSignatureValidationResult(
                false, "Signature mismatch.", "signature_mismatch", diagnostics));

        var handler = CreateHandler(
            signatureValidator: signature.Object,
            mercadoPagoOptions: new MercadoPagoOptions
            {
                WebhookSecret = "configured-secret",
                ApplicationId = "111111",
                UserId = "222222",
                Environment = "Sandbox"
            });

        var result = await handler.Handle(
            new ProcessMercadoPagoPixWebhookCommand(
                "ORDTST01KXGT3VPJ322GGMGAN6P0G9S2",
                "ORDTST01KXGT3VPJ322GGMGAN6P0G9S2",
                "ts=1,v1=abc",
                "req",
                "order.updated",
                "order",
                false,
                "evt-1",
                ApplicationId: "999999",
                UserId: "888888",
                DataStatus: "action_required",
                DataStatusDetail: "waiting_transfer"),
            CancellationToken.None);

        result.StatusCode.Should().Be(401);
        result.Outcome.Should().Be("InvalidSignature");
    }

    [Fact]
    public async Task Handle_MissingBodyApplicationIds_DoesNotBreakValidFlow()
    {
        var orderId = Guid.NewGuid();
        const string providerOrderId = "ORD01NOAPPIDS";
        var payment = CreatePendingPayment(orderId, 10m, providerOrderId);

        var paymentRepo = new Mock<IPixPaymentRepository>();
        paymentRepo.Setup(x => x.GetByProviderOrderIdAsync(providerOrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        var orderClient = new Mock<IMercadoPagoOrderClient>();
        orderClient.Setup(x => x.GetOrderAsync(providerOrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FoundLookup(OrderLookup(providerOrderId, "action_required", "waiting_transfer", orderId, 10m)));

        var handler = CreateHandler(
            signatureValidator: ValidSignature().Object,
            orderClient: orderClient.Object,
            paymentRepository: paymentRepo.Object);

        var result = await handler.Handle(
            new ProcessMercadoPagoPixWebhookCommand(
                providerOrderId, null, "sig", "req", "order.updated", "order", false, null,
                ApplicationId: null, UserId: null, DataStatus: null, DataStatusDetail: null),
            CancellationToken.None);

        result.Outcome.Should().Be("Pending");
        payment.Status.Should().Be(PixPaymentStatus.Pending);
    }

    private static Mock<IMercadoPagoWebhookSignatureValidator> ValidSignature()
    {
        var signature = new Mock<IMercadoPagoWebhookSignatureValidator>();
        signature
            .Setup(x => x.Validate(
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>()))
            .Returns((string? xs, string? xr, string? qd, string? secret) =>
            {
                var hasQ = !string.IsNullOrWhiteSpace(qd);
                var diagnostics = new MercadoPagoWebhookSignatureDiagnostics(
                    HasXSignature: !string.IsNullOrWhiteSpace(xs),
                    HasXRequestId: !string.IsNullOrWhiteSpace(xr),
                    HasQueryDataId: hasQ,
                    DataIdQueryWasLowercased: false,
                    TsPresent: true,
                    V1Present: true,
                    SecretConfigured: !string.IsNullOrWhiteSpace(secret),
                    TimestampAgeSeconds: 0,
                    TimestampWithinTolerance: true,
                    ReceivedV1Prefix: "abcd1234",
                    ComputedOfficialPrefix: "abcd1234",
                    ManifestPartsIncluded: hasQ ? "id/request-id/ts" : "request-id/ts",
                    QueryDataIdMasked: hasQ ? "****" : null,
                    RequestIdMasked: "****",
                    FailureReasonCode: "ok");
                return new MercadoPagoWebhookSignatureValidationResult(true, null, "ok", diagnostics);
            });

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
        IPaymentsPixUnitOfWork? unitOfWork = null,
        MercadoPagoOptions? mercadoPagoOptions = null,
        IMercadoPagoWebhookRawCapture? webhookRawCapture = null)
    {
        var webhookEvents = webhookEventRepository ?? Mock.Of<IMercadoPagoWebhookEventRepository>();
        var uow = unitOfWork ?? Mock.Of<IPaymentsPixUnitOfWork>(x =>
            x.SaveChangesAsync(It.IsAny<CancellationToken>()) == Task.FromResult(1));

        var paidTransition = new MercadoPagoPixPaidTransitionService(
            orderPaidWriter ?? Mock.Of<IOrderPaidWriter>(),
            reservationIdsReader ?? Mock.Of<ICheckoutReservationIdsReader>(),
            reservationConfirmer ?? Mock.Of<IInventoryReservationConfirmer>(),
            uow,
            Mock.Of<Vls.Shopflow.Orders.Application.Interfaces.IOrderEmailNotifier>(),
            NullLogger<MercadoPagoPixPaidTransitionService>.Instance);

        return new ProcessMercadoPagoPixWebhookCommandHandler(
            Options.Create(mercadoPagoOptions ?? new MercadoPagoOptions { WebhookSecret = "secret" }),
            signatureValidator ?? ValidSignature().Object,
            webhookRawCapture ?? Mock.Of<IMercadoPagoWebhookRawCapture>(),
            orderClient ?? Mock.Of<IMercadoPagoOrderClient>(),
            paymentRepository ?? Mock.Of<IPixPaymentRepository>(),
            webhookEvents,
            paidTransition,
            uow,
            NullLogger<ProcessMercadoPagoPixWebhookCommandHandler>.Instance);
    }
}
