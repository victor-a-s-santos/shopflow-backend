using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Vls.Shopflow.PaymentsPix.Application.Interfaces;
using Vls.Shopflow.PaymentsPix.Application.Options;
using Vls.Shopflow.PaymentsPix.Application.Repositories;
using Vls.Shopflow.PaymentsPix.Application.Services;
using Vls.Shopflow.PaymentsPix.Domain.Entities;
using Vls.Shopflow.PaymentsPix.Domain.Enums;

namespace Vls.Shopflow.PaymentsPix.UnitTests.Application;

public sealed class MercadoPagoPixReconciliationProcessorTests
{
    [Fact]
    public async Task ProcessAsync_WhenDisabled_DoesNotQueryRepository()
    {
        var paymentRepo = new Mock<IPixPaymentRepository>();
        var sut = CreateProcessor(paymentRepo.Object, enabled: false);

        var result = await sut.ProcessAsync(CancellationToken.None);

        result.Candidates.Should().Be(0);
        paymentRepo.Verify(
            x => x.GetPendingMercadoPagoForReconciliationBatchAsync(
                It.IsAny<DateTimeOffset>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_RequestsOnlyPendingMercadoPagoBatch()
    {
        var paymentRepo = new Mock<IPixPaymentRepository>();
        paymentRepo.Setup(x => x.GetPendingMercadoPagoForReconciliationBatchAsync(
                It.IsAny<DateTimeOffset>(),
                20,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var sut = CreateProcessor(paymentRepo.Object, enabled: true);
        await sut.ProcessAsync(CancellationToken.None);

        paymentRepo.Verify(
            x => x.GetPendingMercadoPagoForReconciliationBatchAsync(
                It.IsAny<DateTimeOffset>(),
                20,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_IgnoresFakeProviderCandidates_ViaRepositoryFilter()
    {
        // Repository contract must filter Fake; processor must not call GET for empty batch.
        var orderClient = new Mock<IMercadoPagoOrderClient>();
        var paymentRepo = new Mock<IPixPaymentRepository>();
        paymentRepo.Setup(x => x.GetPendingMercadoPagoForReconciliationBatchAsync(
                It.IsAny<DateTimeOffset>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var sut = CreateProcessor(paymentRepo.Object, orderClient.Object, enabled: true);
        await sut.ProcessAsync(CancellationToken.None);

        orderClient.Verify(
            x => x.GetOrderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_WhenGetPending_KeepsPending()
    {
        var orderId = Guid.NewGuid();
        const string providerOrderId = "ORD01RECONPENDING";
        var payment = CreatePendingMercadoPago(orderId, 59.90m, providerOrderId);

        var paymentRepo = new Mock<IPixPaymentRepository>();
        paymentRepo.Setup(x => x.GetPendingMercadoPagoForReconciliationBatchAsync(
                It.IsAny<DateTimeOffset>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([payment]);

        var orderClient = new Mock<IMercadoPagoOrderClient>();
        orderClient.Setup(x => x.GetOrderAsync(providerOrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Found(Lookup(providerOrderId, "action_required", "waiting_transfer", orderId, 59.90m)));

        var paidTransition = new Mock<IMercadoPagoPixPaidTransitionService>();
        var uow = MockUow();

        var sut = CreateProcessor(
            paymentRepo.Object,
            orderClient.Object,
            paidTransition.Object,
            uow.Object,
            enabled: true);

        var result = await sut.ProcessAsync(CancellationToken.None);

        result.StillPending.Should().Be(1);
        result.MarkedPaid.Should().Be(0);
        payment.Status.Should().Be(PixPaymentStatus.Pending);
        paidTransition.Verify(
            x => x.ApplyPaidAsync(It.IsAny<PixPayment>(), It.IsAny<MercadoPagoOrderLookup>(), It.IsAny<CancellationToken>()),
            Times.Never);
        uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WhenGetProcessedAccredited_MarksPaidViaTransition()
    {
        var orderId = Guid.NewGuid();
        const string providerOrderId = "ORD01RECONPAID";
        var payment = CreatePendingMercadoPago(orderId, 59.90m, providerOrderId);

        var paymentRepo = new Mock<IPixPaymentRepository>();
        paymentRepo.Setup(x => x.GetPendingMercadoPagoForReconciliationBatchAsync(
                It.IsAny<DateTimeOffset>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([payment]);

        var mpOrder = Lookup(
            providerOrderId, "processed", "accredited", orderId, 59.90m,
            txStatus: "processed", txDetail: "accredited");

        var orderClient = new Mock<IMercadoPagoOrderClient>();
        orderClient.Setup(x => x.GetOrderAsync(providerOrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Found(mpOrder));

        var paidTransition = new Mock<IMercadoPagoPixPaidTransitionService>();
        paidTransition.Setup(x => x.ApplyPaidAsync(payment, mpOrder, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MercadoPagoPixPaidTransitionResult(true, "Paid", "ok"));

        var sut = CreateProcessor(
            paymentRepo.Object,
            orderClient.Object,
            paidTransition.Object,
            MockUow().Object,
            enabled: true);

        var result = await sut.ProcessAsync(CancellationToken.None);

        result.MarkedPaid.Should().Be(1);
        paidTransition.Verify(
            x => x.ApplyPaidAsync(payment, mpOrder, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_DuplicatePaid_IsIdempotentAlreadyPaid()
    {
        var orderId = Guid.NewGuid();
        const string providerOrderId = "ORD01RECONALREADY";
        var payment = CreatePendingMercadoPago(orderId, 10m, providerOrderId);
        // Simulate race: transition reports AlreadyPaid
        var mpOrder = Lookup(providerOrderId, "processed", "accredited", orderId, 10m,
            txStatus: "processed", txDetail: "accredited");

        var paymentRepo = new Mock<IPixPaymentRepository>();
        paymentRepo.Setup(x => x.GetPendingMercadoPagoForReconciliationBatchAsync(
                It.IsAny<DateTimeOffset>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([payment]);

        var orderClient = new Mock<IMercadoPagoOrderClient>();
        orderClient.Setup(x => x.GetOrderAsync(providerOrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Found(mpOrder));

        var paidTransition = new Mock<IMercadoPagoPixPaidTransitionService>();
        paidTransition.Setup(x => x.ApplyPaidAsync(payment, mpOrder, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MercadoPagoPixPaidTransitionResult(true, "AlreadyPaid", "already"));

        var sut = CreateProcessor(
            paymentRepo.Object,
            orderClient.Object,
            paidTransition.Object,
            MockUow().Object,
            enabled: true);

        var result = await sut.ProcessAsync(CancellationToken.None);
        result.MarkedPaid.Should().Be(1);
    }

    [Fact]
    public async Task ProcessAsync_Get404_DoesNotBreakBatch()
    {
        var orderId = Guid.NewGuid();
        const string providerOrderId = "ORD01MISSING";
        var payment = CreatePendingMercadoPago(orderId, 10m, providerOrderId);

        var paymentRepo = new Mock<IPixPaymentRepository>();
        paymentRepo.Setup(x => x.GetPendingMercadoPagoForReconciliationBatchAsync(
                It.IsAny<DateTimeOffset>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([payment]);

        var orderClient = new Mock<IMercadoPagoOrderClient>();
        orderClient.Setup(x => x.GetOrderAsync(providerOrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MercadoPagoOrderLookupResult(
                MercadoPagoOrderLookupStatus.NotFound, null, 404, "not found"));

        var paidTransition = new Mock<IMercadoPagoPixPaidTransitionService>();
        var sut = CreateProcessor(
            paymentRepo.Object,
            orderClient.Object,
            paidTransition.Object,
            MockUow().Object,
            enabled: true);

        var result = await sut.ProcessAsync(CancellationToken.None);

        result.LookupsSkipped.Should().Be(1);
        result.Failures.Should().Be(0);
        paidTransition.Verify(
            x => x.ApplyPaidAsync(It.IsAny<PixPayment>(), It.IsAny<MercadoPagoOrderLookup>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_GetTransientFailure_SkipsForNextRound()
    {
        var orderId = Guid.NewGuid();
        const string providerOrderId = "ORD01TIMEOUT";
        var payment = CreatePendingMercadoPago(orderId, 10m, providerOrderId);

        var paymentRepo = new Mock<IPixPaymentRepository>();
        paymentRepo.Setup(x => x.GetPendingMercadoPagoForReconciliationBatchAsync(
                It.IsAny<DateTimeOffset>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([payment]);

        var orderClient = new Mock<IMercadoPagoOrderClient>();
        orderClient.Setup(x => x.GetOrderAsync(providerOrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MercadoPagoOrderLookupResult(
                MercadoPagoOrderLookupStatus.TransientFailure, null, 503, "timeout"));

        var sut = CreateProcessor(
            paymentRepo.Object,
            orderClient.Object,
            Mock.Of<IMercadoPagoPixPaidTransitionService>(),
            MockUow().Object,
            enabled: true);

        var result = await sut.ProcessAsync(CancellationToken.None);
        result.LookupsSkipped.Should().Be(1);
        result.Failures.Should().Be(0);
        payment.Status.Should().Be(PixPaymentStatus.Pending);
    }

    [Fact]
    public async Task ApplyPaidAsync_ConfirmsReservationAndMarksOrderAndPayment()
    {
        var orderId = Guid.NewGuid();
        var checkoutSessionId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        const string providerOrderId = "ORD01TRANSITION";
        var payment = CreatePendingMercadoPago(orderId, 25m, providerOrderId);
        var mpOrder = Lookup(providerOrderId, "processed", "accredited", orderId, 25m,
            txStatus: "processed", txDetail: "accredited");

        var orderWriter = new Mock<IOrderPaidWriter>();
        orderWriter.Setup(x => x.GetAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderPaidWriteResult(true, false, false, "PendingPayment", checkoutSessionId));
        orderWriter.Setup(x => x.MarkAsPaidAsync(orderId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderPaidWriteResult(true, false, true, "Paid", checkoutSessionId));

        var reservationReader = new Mock<ICheckoutReservationIdsReader>();
        reservationReader.Setup(x => x.GetReservationIdsByCheckoutSessionAsync(checkoutSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([reservationId]);

        var confirmer = new Mock<IInventoryReservationConfirmer>();
        var uow = MockUow();

        var sut = new MercadoPagoPixPaidTransitionService(
            orderWriter.Object,
            reservationReader.Object,
            confirmer.Object,
            uow.Object,
            NullLogger<MercadoPagoPixPaidTransitionService>.Instance);

        var result = await sut.ApplyPaidAsync(payment, mpOrder, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Outcome.Should().Be("Paid");
        payment.Status.Should().Be(PixPaymentStatus.Paid);
        confirmer.Verify(x => x.ConfirmAsync(reservationId, It.IsAny<CancellationToken>()), Times.Once);
        orderWriter.Verify(
            x => x.MarkAsPaidAsync(orderId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ApplyPaidAsync_WhenAlreadyPaid_StillCallsMarkAsPaidForIntentRepair()
    {
        var orderId = Guid.NewGuid();
        var checkoutSessionId = Guid.NewGuid();
        const string providerOrderId = "ORD01ALREADYPAID";
        var payment = CreatePendingMercadoPago(orderId, 25m, providerOrderId);
        payment.MarkAsPaid("processed", "accredited", "processed", "accredited", DateTimeOffset.UtcNow, providerOrderId, "tx-1");
        var mpOrder = Lookup(providerOrderId, "processed", "accredited", orderId, 25m,
            txStatus: "processed", txDetail: "accredited");

        var orderWriter = new Mock<IOrderPaidWriter>();
        orderWriter.Setup(x => x.GetAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderPaidWriteResult(true, true, false, "Paid", checkoutSessionId));
        orderWriter.Setup(x => x.MarkAsPaidAsync(orderId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderPaidWriteResult(true, true, false, "Paid", checkoutSessionId));

        var reservationReader = new Mock<ICheckoutReservationIdsReader>();
        reservationReader.Setup(x => x.GetReservationIdsByCheckoutSessionAsync(checkoutSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var sut = new MercadoPagoPixPaidTransitionService(
            orderWriter.Object,
            reservationReader.Object,
            Mock.Of<IInventoryReservationConfirmer>(),
            MockUow().Object,
            NullLogger<MercadoPagoPixPaidTransitionService>.Instance);

        var result = await sut.ApplyPaidAsync(payment, mpOrder, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Outcome.Should().Be("AlreadyPaid");
        orderWriter.Verify(
            x => x.MarkAsPaidAsync(orderId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static MercadoPagoPixReconciliationProcessor CreateProcessor(
        IPixPaymentRepository paymentRepository,
        bool enabled)
        => CreateProcessor(
            paymentRepository,
            Mock.Of<IMercadoPagoOrderClient>(),
            Mock.Of<IMercadoPagoPixPaidTransitionService>(),
            MockUow().Object,
            enabled);

    private static MercadoPagoPixReconciliationProcessor CreateProcessor(
        IPixPaymentRepository paymentRepository,
        IMercadoPagoOrderClient orderClient,
        bool enabled)
        => CreateProcessor(
            paymentRepository,
            orderClient,
            Mock.Of<IMercadoPagoPixPaidTransitionService>(),
            MockUow().Object,
            enabled);

    private static MercadoPagoPixReconciliationProcessor CreateProcessor(
        IPixPaymentRepository paymentRepository,
        IMercadoPagoOrderClient orderClient,
        IMercadoPagoPixPaidTransitionService paidTransition,
        IPaymentsPixUnitOfWork unitOfWork,
        bool enabled)
        => new(
            Options.Create(new MercadoPagoReconciliationOptions
            {
                Enabled = enabled,
                BatchSize = 20,
                MaxAgeMinutes = 180
            }),
            paymentRepository,
            orderClient,
            paidTransition,
            unitOfWork,
            NullLogger<MercadoPagoPixReconciliationProcessor>.Instance);

    private static Mock<IPaymentsPixUnitOfWork> MockUow()
    {
        var uow = new Mock<IPaymentsPixUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        return uow;
    }

    private static PixPayment CreatePendingMercadoPago(Guid orderId, decimal amount, string providerOrderId)
        => PixPayment.CreatePending(
            orderId,
            amount,
            PixPaymentProviderType.MercadoPago,
            providerOrderId,
            $"PAY-{providerOrderId}",
            null,
            null,
            "qr",
            null,
            "action_required",
            "waiting_transfer",
            "action_required",
            "waiting_transfer",
            orderId.ToString("D"),
            orderId.ToString("D"),
            DateTimeOffset.UtcNow.AddMinutes(30));

    private static MercadoPagoOrderLookup Lookup(
        string id,
        string status,
        string statusDetail,
        Guid orderId,
        decimal amount,
        string? txStatus = null,
        string? txDetail = null)
        => new(
            id,
            status,
            statusDetail,
            orderId.ToString("D"),
            amount,
            $"PAY-{id}",
            amount,
            txStatus,
            txDetail,
            "pix",
            "bank_transfer",
            null,
            null,
            null,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

    private static MercadoPagoOrderLookupResult Found(MercadoPagoOrderLookup order)
        => new(MercadoPagoOrderLookupStatus.Found, order, 200, null);
}
