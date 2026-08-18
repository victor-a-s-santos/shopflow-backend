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
        var manifest = ManualMercadoPagoWebhookSignatureValidator.BuildManifestFromRaw(dataId, requestId, ts);
        var v1 = ManualMercadoPagoWebhookSignatureValidator.ComputeHmacHex(secret, manifest);

        var validator = CreateValidator();
        var valid = validator.IsValid($"ts={ts},v1={v1}", requestId, dataId, secret, out var reason);

        valid.Should().BeTrue();
        reason.Should().BeNull();
    }

    [Fact]
    public void Validate_WithMillisecondTimestamp_ReturnsTrue()
    {
        var secret = "test-webhook-secret";
        var dataId = "ORD01JQ4S4KY8HWQ6NA5PXB65B3D3";
        var requestId = "2066ca19-c6f1-498a-be75-1923005edd06";
        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var manifest = ManualMercadoPagoWebhookSignatureValidator.BuildManifestFromRaw(dataId, requestId, ts);
        var v1 = ManualMercadoPagoWebhookSignatureValidator.ComputeHmacHex(secret, manifest);

        var result = CreateValidator().Validate($"ts={ts},v1={v1}", requestId, dataId, secret);

        result.IsValid.Should().BeTrue();
        result.Diagnostics.TimestampWithinTolerance.Should().BeTrue();
        manifest.Should().Be(
            $"id:ord01jq4s4ky8hwq6na5pxb65b3d3;request-id:{requestId};ts:{ts};");
    }

    [Fact]
    public void Validate_OmitsMissingRequestIdFromManifest()
    {
        var secret = "test-webhook-secret";
        var dataId = "123456";
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var manifest = ManualMercadoPagoWebhookSignatureValidator.BuildManifest(
            ManualMercadoPagoWebhookSignatureValidator.NormalizeDataIdForManifest(dataId),
            requestIdOrNull: null,
            ts);
        var v1 = ManualMercadoPagoWebhookSignatureValidator.ComputeHmacHex(secret, manifest);

        var result = CreateValidator().Validate($"ts={ts},v1={v1}", xRequestId: null, dataId, secret);

        result.IsValid.Should().BeTrue();
        result.Diagnostics.ManifestPartsIncluded.Should().Be("id/ts");
        manifest.Should().Be($"id:123456;ts:{ts};");
    }

    [Fact]
    public void Validate_OmitsMissingDataIdFromManifest()
    {
        var secret = "test-webhook-secret";
        var requestId = "abc-request-id";
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var manifest = ManualMercadoPagoWebhookSignatureValidator.BuildManifest(null, requestId, ts);
        var v1 = ManualMercadoPagoWebhookSignatureValidator.ComputeHmacHex(secret, manifest);

        var result = CreateValidator().Validate($"ts={ts},v1={v1}", requestId, queryDataId: null, secret);

        result.IsValid.Should().BeTrue();
        result.Diagnostics.ManifestPartsIncluded.Should().Be("request-id/ts");
        manifest.Should().Be($"request-id:{requestId};ts:{ts};");
    }

    [Fact]
    public void Validate_BodyDataIdDoesNotAffectHmac()
    {
        var secret = "test-webhook-secret";
        var queryId = "ORD01QUERYONLY";
        var bodyId = "ORD01DIFFERENTBODY";
        var requestId = "req-1";
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var manifest = ManualMercadoPagoWebhookSignatureValidator.BuildManifestFromRaw(queryId, requestId, ts);
        var v1 = ManualMercadoPagoWebhookSignatureValidator.ComputeHmacHex(secret, manifest);

        var result = CreateValidator().Validate($"ts={ts},v1={v1}", requestId, queryId, secret);

        result.IsValid.Should().BeTrue();

        // Signing with body id would not match the query-based v1.
        var bodyManifest = ManualMercadoPagoWebhookSignatureValidator.BuildManifestFromRaw(bodyId, requestId, ts);
        var bodyV1 = ManualMercadoPagoWebhookSignatureValidator.ComputeHmacHex(secret, bodyManifest);
        bodyV1.Should().NotBe(v1);
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
    public void Validate_WithMissingTs_ReturnsFalse()
    {
        var result = CreateValidator().Validate(
            "v1=618c85345248dd820d5fd456117c2ab2ef8eda45a0282ff693eac24131a5e839",
            "req",
            "123",
            "secret");

        result.IsValid.Should().BeFalse();
        result.FailureReasonCode.Should().Be("missing_ts");
    }

    [Fact]
    public void Validate_WithMissingV1_ReturnsFalse()
    {
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var result = CreateValidator().Validate(
            $"ts={ts}",
            "req",
            "123",
            "secret");

        result.IsValid.Should().BeFalse();
        result.FailureReasonCode.Should().Be("missing_v1");
    }

    [Fact]
    public void BuildManifest_UsesOfficialFormat()
    {
        ManualMercadoPagoWebhookSignatureValidator.BuildManifestFromRaw("99", "req-1", "1700000000")
            .Should().Be("id:99;request-id:req-1;ts:1700000000;");
    }

    [Fact]
    public void BuildManifest_LowercasesAlphanumericOrderIds()
    {
        ManualMercadoPagoWebhookSignatureValidator.BuildManifestFromRaw("ORD01ABC", "req-1", "1700000000")
            .Should().Be("id:ord01abc;request-id:req-1;ts:1700000000;");
    }

    [Fact]
    public void IsValid_WithUppercaseOrderId_UsesLowercaseInManifest()
    {
        var secret = "test-webhook-secret";
        var dataId = "ORD01JP84C939T20S0P1DN382FQ6K";
        var requestId = "abc-request-id";
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var manifest = ManualMercadoPagoWebhookSignatureValidator.BuildManifestFromRaw(dataId, requestId, ts);
        var v1 = ManualMercadoPagoWebhookSignatureValidator.ComputeHmacHex(secret, manifest);

        var validator = CreateValidator();
        var valid = validator.IsValid($"ts={ts},v1={v1}", requestId, dataId, secret, out var reason);

        valid.Should().BeTrue();
        reason.Should().BeNull();
        manifest.Should().Contain("id:ord01jp84c939t20s0p1dn382fq6k;");
    }

    [Fact]
    public void IsValid_WithOrdTstUppercase_UsesLowercaseInManifest()
    {
        var secret = "test-webhook-secret";
        var dataId = "ORDTST01KXGT3VPJ322GGMGAN6P0G9S2";
        var requestId = "abc-request-id";
        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var manifest = ManualMercadoPagoWebhookSignatureValidator.BuildManifestFromRaw(dataId, requestId, ts);
        var v1 = ManualMercadoPagoWebhookSignatureValidator.ComputeHmacHex(secret, manifest);

        var result = CreateValidator().Validate($"ts={ts},v1={v1}", requestId, dataId, secret);

        result.IsValid.Should().BeTrue();
        manifest.Should().StartWith("id:ordtst01kxgt3vpj322ggmgan6p0g9s2;");
    }

    [Fact]
    public void FixedTimeEqualsHex_RejectsDifferentValues()
    {
        var a = ManualMercadoPagoWebhookSignatureValidator.ComputeHmacHex("secret", "manifest-a");
        var b = ManualMercadoPagoWebhookSignatureValidator.ComputeHmacHex("secret", "manifest-b");

        ManualMercadoPagoWebhookSignatureValidator.FixedTimeEqualsHex(a, b).Should().BeFalse();
        ManualMercadoPagoWebhookSignatureValidator.FixedTimeEqualsHex(a, a).Should().BeTrue();
    }

    [Fact]
    public void IsValid_WithExpiredTimestamp_ReturnsFalse()
    {
        var secret = "test-webhook-secret";
        var dataId = "123456";
        var requestId = "abc-request-id";
        var ts = DateTimeOffset.UtcNow.AddMinutes(-30).ToUnixTimeSeconds().ToString();
        var manifest = ManualMercadoPagoWebhookSignatureValidator.BuildManifestFromRaw(dataId, requestId, ts);
        var v1 = ManualMercadoPagoWebhookSignatureValidator.ComputeHmacHex(secret, manifest);

        var validator = CreateValidator(toleranceMinutes: 10);
        var valid = validator.IsValid($"ts={ts},v1={v1}", requestId, dataId, secret, out var reason);

        valid.Should().BeFalse();
        reason.Should().Be("Signature timestamp outside tolerance window.");
    }

    private static ManualMercadoPagoWebhookSignatureValidator CreateValidator(int toleranceMinutes = 10)
        => new(Options.Create(new MercadoPagoOptions
        {
            WebhookSignatureToleranceMinutes = toleranceMinutes
        }));
}
