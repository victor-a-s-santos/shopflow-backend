using FluentAssertions;
using Microsoft.Extensions.Options;
using Vls.Shopflow.PaymentsPix.Application.Options;
using Vls.Shopflow.PaymentsPix.Infrastructure.MercadoPago;

namespace Vls.Shopflow.PaymentsPix.UnitTests.Infrastructure;

public sealed class MercadoPagoWebhookSignatureValidatorTests
{
    [Fact]
    public void IsValid_WithCorrectSignature_ReturnsTrue()
    {
        var secret = "test-webhook-secret";
        var dataId = "123456";
        var requestId = "abc-request-id";
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var manifest = MercadoPagoWebhookSignatureValidator.BuildManifest(dataId, requestId, ts);
        var v1 = MercadoPagoWebhookSignatureValidator.ComputeHmacHex(secret, manifest);

        var validator = CreateValidator();
        var valid = validator.IsValid($"ts={ts},v1={v1}", requestId, dataId, secret, out var reason);

        valid.Should().BeTrue();
        reason.Should().BeNull();
    }

    [Fact]
    public void IsValid_WithInvalidV1_ReturnsFalse()
    {
        var secret = "test-webhook-secret";
        var dataId = "123456";
        var requestId = "abc-request-id";
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        var validator = CreateValidator();
        var valid = validator.IsValid(
            $"ts={ts},v1=0000000000000000000000000000000000000000000000000000000000000000",
            requestId,
            dataId,
            secret,
            out var reason);

        valid.Should().BeFalse();
        reason.Should().Be("Signature mismatch.");
    }

    [Fact]
    public void IsValid_WithMissingHeader_ReturnsFalse()
    {
        var validator = CreateValidator();
        var valid = validator.IsValid(null, "req", "123", "secret", out var reason);

        valid.Should().BeFalse();
        reason.Should().Be("Missing x-signature header.");
    }

    [Fact]
    public void BuildManifest_UsesOfficialFormat()
    {
        MercadoPagoWebhookSignatureValidator.BuildManifest("99", "req-1", "1700000000")
            .Should().Be("id:99;request-id:req-1;ts:1700000000;");
    }

    [Fact]
    public void BuildManifest_LowercasesAlphanumericOrderIds()
    {
        MercadoPagoWebhookSignatureValidator.BuildManifest("ORD01ABC", "req-1", "1700000000")
            .Should().Be("id:ord01abc;request-id:req-1;ts:1700000000;");
    }

    [Fact]
    public void IsValid_WithUppercaseOrderId_UsesLowercaseInManifest()
    {
        var secret = "test-webhook-secret";
        var dataId = "ORD01JP84C939T20S0P1DN382FQ6K";
        var requestId = "abc-request-id";
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var manifest = MercadoPagoWebhookSignatureValidator.BuildManifest(dataId, requestId, ts);
        var v1 = MercadoPagoWebhookSignatureValidator.ComputeHmacHex(secret, manifest);

        var validator = CreateValidator();
        var valid = validator.IsValid($"ts={ts},v1={v1}", requestId, dataId, secret, out var reason);

        valid.Should().BeTrue();
        reason.Should().BeNull();
        manifest.Should().Contain("id:ord01jp84c939t20s0p1dn382fq6k;");
    }

    [Fact]
    public void FixedTimeEqualsHex_RejectsDifferentValues()
    {
        var a = MercadoPagoWebhookSignatureValidator.ComputeHmacHex("secret", "manifest-a");
        var b = MercadoPagoWebhookSignatureValidator.ComputeHmacHex("secret", "manifest-b");

        MercadoPagoWebhookSignatureValidator.FixedTimeEqualsHex(a, b).Should().BeFalse();
        MercadoPagoWebhookSignatureValidator.FixedTimeEqualsHex(a, a).Should().BeTrue();
    }

    [Fact]
    public void IsValid_WithExpiredTimestamp_ReturnsFalse()
    {
        var secret = "test-webhook-secret";
        var dataId = "123456";
        var requestId = "abc-request-id";
        var ts = DateTimeOffset.UtcNow.AddMinutes(-30).ToUnixTimeSeconds().ToString();
        var manifest = MercadoPagoWebhookSignatureValidator.BuildManifest(dataId, requestId, ts);
        var v1 = MercadoPagoWebhookSignatureValidator.ComputeHmacHex(secret, manifest);

        var validator = CreateValidator(toleranceMinutes: 10);
        var valid = validator.IsValid($"ts={ts},v1={v1}", requestId, dataId, secret, out var reason);

        valid.Should().BeFalse();
        reason.Should().Be("Signature timestamp outside tolerance window.");
    }

    private static MercadoPagoWebhookSignatureValidator CreateValidator(int toleranceMinutes = 10)
        => new(Options.Create(new MercadoPagoOptions
        {
            WebhookSignatureToleranceMinutes = toleranceMinutes
        }));
}
