using FluentAssertions;
using Microsoft.Extensions.Options;
using Vls.Shopflow.Orders.Application.Options;
using Vls.Shopflow.Orders.Domain.Exceptions;
using Vls.Shopflow.Orders.Infrastructure.Services;

namespace Vls.Shopflow.Orders.UnitTests.Infrastructure;

public sealed class GuestOrderAccessTokenHasherTests
{
    [Fact]
    public void GenerateRawToken_IsBase64UrlAndAtLeast256BitsDecoded()
    {
        var hasher = CreateHasher();
        var token = hasher.GenerateRawToken();

        token.Should().NotContain("+");
        token.Should().NotContain("/");
        token.Should().NotContain("=");

        var padded = token.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }

        var bytes = Convert.FromBase64String(padded);
        bytes.Length.Should().BeGreaterOrEqualTo(32);
    }

    [Fact]
    public void Hash_DoesNotEqualRawToken_AndIsDeterministic()
    {
        var hasher = CreateHasher("super-secret");
        var raw = hasher.GenerateRawToken();
        var hash1 = hasher.Hash(raw);
        var hash2 = hasher.Hash(raw);

        hash1.Should().Be(hash2);
        hash1.Should().NotBe(raw);
        hash1.Should().HaveLength(64);
    }

    [Fact]
    public void Hash_WhenSecretMissing_Throws()
    {
        var hasher = CreateHasher("");
        var act = () => hasher.Hash("any");
        act.Should().Throw<GuestOrderAccessMisconfiguredException>();
    }

    private static GuestOrderAccessTokenHasher CreateHasher(string secret = "test-secret")
        => new(Options.Create(new GuestOrderAccessOptions { TokenHashSecret = secret }));
}
