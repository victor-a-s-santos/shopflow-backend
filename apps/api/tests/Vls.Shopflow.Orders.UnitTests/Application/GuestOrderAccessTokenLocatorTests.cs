using FluentAssertions;
using Vls.Shopflow.Orders.Application.Services;

namespace Vls.Shopflow.Orders.UnitTests.Application;

public sealed class GuestOrderAccessTokenLocatorTests
{
    [Fact]
    public void Resolve_PrefersHeader_OverQuery()
    {
        var resolved = GuestOrderAccessTokenLocator.Resolve(" header-token ", "t-token", "token-alias");
        resolved.Should().Be("header-token");
    }

    [Fact]
    public void Resolve_UsesEmailQueryT_WhenHeaderMissing()
    {
        var resolved = GuestOrderAccessTokenLocator.Resolve("  ", "email-t", "legacy");
        resolved.Should().Be("email-t");
    }

    [Fact]
    public void Resolve_UsesLegacyTokenQuery_WhenHeaderAndTMissing()
    {
        var resolved = GuestOrderAccessTokenLocator.Resolve(null, null, " legacy ");
        resolved.Should().Be("legacy");
    }

    [Fact]
    public void Resolve_ReturnsNull_WhenAllMissing()
    {
        GuestOrderAccessTokenLocator.Resolve(null, " ", "").Should().BeNull();
    }
}
