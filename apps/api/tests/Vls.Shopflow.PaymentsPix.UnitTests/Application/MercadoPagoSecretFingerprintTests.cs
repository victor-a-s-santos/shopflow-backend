using FluentAssertions;
using Vls.Shopflow.PaymentsPix.Application.Security;

namespace Vls.Shopflow.PaymentsPix.UnitTests.Application;

public sealed class MercadoPagoSecretFingerprintTests
{
    [Fact]
    public void Compute_ReturnsFirst8HexCharsOfSha256()
    {
        var fingerprint = MercadoPagoSecretFingerprint.Compute("my-webhook-secret");

        fingerprint.Should().NotBeNullOrEmpty();
        fingerprint.Should().HaveLength(8);
        fingerprint.Should().MatchRegex("^[0-9a-f]{8}$");
        fingerprint.Should().NotContain("my-webhook-secret");
    }

    [Fact]
    public void Compute_IsStableForSameSecret()
    {
        MercadoPagoSecretFingerprint.Compute("same-secret")
            .Should().Be(MercadoPagoSecretFingerprint.Compute("same-secret"));
    }

    [Fact]
    public void Compute_DiffersForDifferentSecrets()
    {
        MercadoPagoSecretFingerprint.Compute("secret-a")
            .Should().NotBe(MercadoPagoSecretFingerprint.Compute("secret-b"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Compute_Empty_ReturnsNull(string? secret)
    {
        MercadoPagoSecretFingerprint.Compute(secret).Should().BeNull();
    }
}
