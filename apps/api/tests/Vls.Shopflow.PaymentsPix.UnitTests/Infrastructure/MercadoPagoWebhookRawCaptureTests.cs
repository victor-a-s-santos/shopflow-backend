using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Vls.Shopflow.PaymentsPix.Application.Interfaces;
using Vls.Shopflow.PaymentsPix.Application.Options;
using Vls.Shopflow.PaymentsPix.Infrastructure.MercadoPago;

namespace Vls.Shopflow.PaymentsPix.UnitTests.Infrastructure;

public sealed class MercadoPagoWebhookRawCaptureTests
{
    [Fact]
    public void TryCapture_Production_NeverLogsEvenWhenEnabled()
    {
        var logger = new Mock<ILogger<MercadoPagoWebhookRawCapture>>();
        var sut = CreateSut(logger.Object, "Production", enabled: true);

        sut.TryCapture(SampleInput(queryDataId: "ORDTST01X"), RejectedSignature());

        logger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("MP_WEBHOOK_RAW_CAPTURE")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public void TryCapture_Testing_WhenEnabled_LogsCaptureIncludingXSignature()
    {
        var logger = new Mock<ILogger<MercadoPagoWebhookRawCapture>>();
        var sut = CreateSut(
            logger.Object,
            "Testing",
            enabled: true,
            secret: "test-secret-value");

        const string signature = "ts=1742505638683,v1=abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
        sut.TryCapture(
            SampleInput(queryDataId: "ORDTST01CAPTURE", xSignature: signature),
            RejectedSignature());

        logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) =>
                    v.ToString()!.Contains("MP_WEBHOOK_RAW_CAPTURE")
                    && v.ToString()!.Contains(signature)
                    && !v.ToString()!.Contains("test-secret-value")
                    && !v.ToString()!.Contains("APP_USR-TOKEN")
                    && !v.ToString()!.Contains("AccessToken")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void TryCapture_Disabled_DoesNotLog()
    {
        var logger = new Mock<ILogger<MercadoPagoWebhookRawCapture>>();
        var sut = CreateSut(logger.Object, "Testing", enabled: false);

        sut.TryCapture(SampleInput(queryDataId: "ORDTST01X"), RejectedSignature());

        VerifyNeverCaptured(logger);
    }

    [Fact]
    public void TryCapture_OrderFilter_SkipsOtherOrders()
    {
        var logger = new Mock<ILogger<MercadoPagoWebhookRawCapture>>();
        var sut = CreateSut(logger.Object, "Staging", enabled: true, orderFilter: "ORDTST01TARGET");

        sut.TryCapture(SampleInput(queryDataId: "ORDTST01OTHER"), RejectedSignature());
        VerifyNeverCaptured(logger);

        sut.TryCapture(SampleInput(queryDataId: "ORDTST01TARGET"), RejectedSignature());
        logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("MP_WEBHOOK_RAW_CAPTURE")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void TryCapture_MaxEvents_LimitsCaptures()
    {
        var logger = new Mock<ILogger<MercadoPagoWebhookRawCapture>>();
        var sut = CreateSut(logger.Object, "Testing", enabled: true, maxEvents: 2);

        sut.TryCapture(SampleInput(queryDataId: "ORD1"), RejectedSignature());
        sut.TryCapture(SampleInput(queryDataId: "ORD2"), RejectedSignature());
        sut.TryCapture(SampleInput(queryDataId: "ORD3"), RejectedSignature());

        logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("MP_WEBHOOK_RAW_CAPTURE")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Exactly(2));
    }

    [Fact]
    public void TryCapture_DoesNotEmbedWebhookSecretInLogState()
    {
        object? state = null;
        var logger = new Mock<ILogger<MercadoPagoWebhookRawCapture>>();
        logger.Setup(x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback((LogLevel _, EventId _, object s, Exception? _, Delegate _) => state = s);

        var secret = "super-secret-webhook-key-xyz";
        var sut = CreateSut(logger.Object, "Testing", enabled: true, secret: secret);
        sut.TryCapture(SampleInput(queryDataId: "ORDTST01X"), RejectedSignature());

        state.Should().NotBeNull();
        var text = state!.ToString()!;
        text.Should().NotContain(secret);
        text.Should().Contain("webhook_secret_fingerprint");
    }

    private static MercadoPagoWebhookRawCapture CreateSut(
        ILogger<MercadoPagoWebhookRawCapture> logger,
        string environmentName,
        bool enabled,
        string? orderFilter = null,
        int maxEvents = 5,
        string secret = "secret")
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(x => x.EnvironmentName).Returns(environmentName);

        return new MercadoPagoWebhookRawCapture(
            env.Object,
            Options.Create(new MercadoPagoOptions
            {
                WebhookRawCaptureEnabled = enabled,
                WebhookRawCaptureOrderId = orderFilter,
                WebhookRawCaptureMaxEvents = maxEvents,
                WebhookSecret = secret,
                ApplicationId = "111",
                UserId = "222",
                Environment = "Sandbox"
            }),
            logger);
    }

    private static MercadoPagoWebhookRawCaptureInput SampleInput(
        string queryDataId,
        string? xSignature = "ts=1,v1=abc")
        => new(
            DateTimeOffset.UtcNow,
            "POST",
            "/api/payments/pix/webhooks/mercado-pago",
            $"?data.id={queryDataId}&type=order",
            queryDataId,
            "order",
            "req-1",
            xSignature,
            """{"id":"1","type":"order","data":{"id":"ORDTST01X"},"application_id":"111","user_id":"222","live_mode":false}""",
            "111",
            "222",
            false,
            "order",
            "order.updated",
            "ORDTST01X",
            "action_required",
            "waiting_transfer");

    private static MercadoPagoWebhookSignatureValidationResult RejectedSignature()
    {
        var diagnostics = new MercadoPagoWebhookSignatureDiagnostics(
            true, true, true, true, true, true, true, 1, true, "aabbccdd", "11223344",
            "id/request-id/ts", "ORD***", "req***", "signature_mismatch",
            SdkSignatureValid: false,
            ManualSignatureValid: false,
            SignatureValidatorFinal: "Rejected",
            SdkExceptionType: "InvalidWebhookSignatureException",
            ManualFailureReason: "Signature mismatch.",
            SecretLength: 6,
            SecretTrimmedChanged: false,
            WebhookSecretFingerprint: "deadbeef");
        return new MercadoPagoWebhookSignatureValidationResult(
            false, "Signature mismatch.", "signature_mismatch", diagnostics);
    }

    private static void VerifyNeverCaptured(Mock<ILogger<MercadoPagoWebhookRawCapture>> logger)
        => logger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("MP_WEBHOOK_RAW_CAPTURE")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
}
