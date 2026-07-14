using FluentAssertions;
using MercadoPago.Error;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Vls.Shopflow.PaymentsPix.Application.Interfaces;
using Vls.Shopflow.PaymentsPix.Application.Options;
using Vls.Shopflow.PaymentsPix.Infrastructure.MercadoPago;

namespace Vls.Shopflow.PaymentsPix.UnitTests.Infrastructure;

public sealed class CompositeMercadoPagoWebhookSignatureValidatorTests
{
    [Fact]
    public void Validate_SdkAcceptsManualRejects_PrefersSdk()
    {
        var sdk = new Mock<IMercadoPagoOfficialWebhookSignatureClient>();
        // SDK succeeds (no throw)
        sdk.Setup(x => x.Validate(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<TimeSpan?>()));

        var sut = CreateSut(sdk.Object, secret: "secret");
        var dataId = "ORDTST01UPPERCASEID";
        // Sign with lowercase manifest for "manual" path (secret matches but SDK receives raw uppercase).
        // Manual lowercases → HMAC for ordtst…; we compute signature for uppercase id as SDK would.
        var requestId = "req-1";
        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var sdkManifest = $"id:{dataId};request-id:{requestId};ts:{ts};";
        var v1 = ManualMercadoPagoWebhookSignatureValidator.ComputeHmacHex("secret", sdkManifest);

        var result = sut.Validate($"ts={ts},v1={v1}", requestId, dataId, "secret");

        result.IsValid.Should().BeTrue();
        result.Diagnostics.SignatureValidatorFinal.Should().Be("Sdk");
        result.Diagnostics.SdkSignatureValid.Should().BeTrue();
        // Manual lowercases ORD* so its HMAC differs from the case-preserving signature.
        result.Diagnostics.ManualSignatureValid.Should().BeFalse();
        sdk.Verify(x => x.Validate(
            $"ts={ts},v1={v1}",
            requestId,
            dataId,
            "secret",
            It.IsAny<TimeSpan?>()), Times.Once);
    }

    [Fact]
    public void Validate_SdkRejectsManualAccepts_RejectsWith401Semantics()
    {
        var sdk = new Mock<IMercadoPagoOfficialWebhookSignatureClient>();
        sdk.Setup(x => x.Validate(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<TimeSpan?>()))
            .Throws(new InvalidWebhookSignatureException(
                SignatureFailureReason.SignatureMismatch,
                "req-1",
                "1700000000"));

        var sut = CreateSut(sdk.Object, secret: "secret");
        var dataId = "ORD01ABC";
        var requestId = "req-1";
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var manualManifest = ManualMercadoPagoWebhookSignatureValidator.BuildManifestFromRaw(dataId, requestId, ts);
        var v1 = ManualMercadoPagoWebhookSignatureValidator.ComputeHmacHex("secret", manualManifest);

        var result = sut.Validate($"ts={ts},v1={v1}", requestId, dataId, "secret");

        result.IsValid.Should().BeFalse();
        result.Diagnostics.SignatureValidatorFinal.Should().Be("Rejected");
        result.Diagnostics.SdkSignatureValid.Should().BeFalse();
        result.Diagnostics.ManualSignatureValid.Should().BeTrue();
        result.FailureReasonCode.Should().Be("signature_mismatch");
    }

    [Fact]
    public void Validate_BothReject_ReturnsRejected()
    {
        var sdk = new Mock<IMercadoPagoOfficialWebhookSignatureClient>();
        sdk.Setup(x => x.Validate(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<TimeSpan?>()))
            .Throws(new InvalidWebhookSignatureException(
                SignatureFailureReason.SignatureMismatch,
                "req-1",
                "1700000000"));

        var sut = CreateSut(sdk.Object, secret: "secret");
        var result = sut.Validate(
            "ts=1700000000,v1=0000000000000000000000000000000000000000000000000000000000000000",
            "req-1",
            "ORD01ABC",
            "secret");

        result.IsValid.Should().BeFalse();
        result.Diagnostics.SdkSignatureValid.Should().BeFalse();
        result.Diagnostics.ManualSignatureValid.Should().BeFalse();
        result.Diagnostics.SignatureValidatorFinal.Should().Be("Rejected");
    }

    [Fact]
    public void Validate_SdkUnavailable_FallsBackToManual()
    {
        var sdk = new Mock<IMercadoPagoOfficialWebhookSignatureClient>();
        sdk.Setup(x => x.Validate(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<TimeSpan?>()))
            .Throws(new InvalidOperationException("boom"));

        var sut = CreateSut(sdk.Object, secret: "secret");
        var dataId = "123456";
        var requestId = "req-1";
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var manifest = ManualMercadoPagoWebhookSignatureValidator.BuildManifestFromRaw(dataId, requestId, ts);
        var v1 = ManualMercadoPagoWebhookSignatureValidator.ComputeHmacHex("secret", manifest);

        var result = sut.Validate($"ts={ts},v1={v1}", requestId, dataId, "secret");

        result.IsValid.Should().BeTrue();
        result.Diagnostics.SignatureValidatorFinal.Should().Be("ManualFallback");
        result.Diagnostics.SdkSignatureValid.Should().BeNull();
        result.Diagnostics.ManualSignatureValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_TrimsSecretAndFlagsTrimmedChanged()
    {
        var capturedSecret = string.Empty;
        var sdk = new Mock<IMercadoPagoOfficialWebhookSignatureClient>();
        sdk.Setup(x => x.Validate(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<TimeSpan?>()))
            .Callback<string, string?, string?, string, TimeSpan?>((_, _, _, secret, _) => capturedSecret = secret);

        var sut = CreateSut(sdk.Object, secret: "secret");
        var dataId = "123456";
        var requestId = "req-1";
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var manifest = ManualMercadoPagoWebhookSignatureValidator.BuildManifestFromRaw(dataId, requestId, ts);
        var v1 = ManualMercadoPagoWebhookSignatureValidator.ComputeHmacHex("secret", manifest);

        var result = sut.Validate($"ts={ts},v1={v1}", requestId, dataId, "  secret\n");

        result.IsValid.Should().BeTrue();
        capturedSecret.Should().Be("secret");
        result.Diagnostics.SecretTrimmedChanged.Should().BeTrue();
        result.Diagnostics.SecretLength.Should().Be("secret".Length);
        result.Diagnostics.WebhookSecretFingerprint.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Validate_PassesQueryDataIdNotBodyToSdk()
    {
        string? capturedDataId = null;
        var sdk = new Mock<IMercadoPagoOfficialWebhookSignatureClient>();
        sdk.Setup(x => x.Validate(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<TimeSpan?>()))
            .Callback<string, string?, string?, string, TimeSpan?>((_, _, dataId, _, _) => capturedDataId = dataId);

        var sut = CreateSut(sdk.Object, secret: "secret");
        const string queryId = "ORD01FROMQUERY";
        var requestId = "req-1";
        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var v1 = ManualMercadoPagoWebhookSignatureValidator.ComputeHmacHex(
            "secret",
            $"id:{queryId};request-id:{requestId};ts:{ts};");

        sut.Validate($"ts={ts},v1={v1}", requestId, queryId, "secret");

        capturedDataId.Should().Be(queryId);
        capturedDataId.Should().NotBe("ORD01FROMBODY");
    }

    private static CompositeMercadoPagoWebhookSignatureValidator CreateSut(
        IMercadoPagoOfficialWebhookSignatureClient sdk,
        string secret)
    {
        var options = Options.Create(new MercadoPagoOptions
        {
            WebhookSecret = secret,
            WebhookSignatureToleranceMinutes = 10
        });
        return new CompositeMercadoPagoWebhookSignatureValidator(
            sdk,
            new ManualMercadoPagoWebhookSignatureValidator(options),
            options,
            NullLogger<CompositeMercadoPagoWebhookSignatureValidator>.Instance);
    }
}
