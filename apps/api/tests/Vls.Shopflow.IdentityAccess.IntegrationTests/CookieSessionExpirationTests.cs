using Microsoft.AspNetCore.Authentication;
using FluentAssertions;
using Vls.Shopflow.IdentityAccess.Infrastructure.Authentication;
using Vls.Shopflow.IdentityAccess.Infrastructure.Options;

namespace Vls.Shopflow.IdentityAccess.IntegrationTests;

public sealed class CookieSessionExpirationTests
{
    [Fact]
    public void Stamp_RoundTripsStartedAndAbsoluteLifetime()
    {
        var properties = new AuthenticationProperties();
        var started = new DateTimeOffset(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);

        CookieSessionExpiration.Stamp(properties, started, TimeSpan.FromHours(8));

        CookieSessionExpiration.TryGetSessionStarted(properties, out var readStarted).Should().BeTrue();
        readStarted.Should().Be(started);
        CookieSessionExpiration.TryGetAbsoluteLifetime(properties, out var lifetime).Should().BeTrue();
        lifetime.Should().Be(TimeSpan.FromHours(8));
    }

    [Fact]
    public void IsAbsoluteExpired_IsFalse_BeforeLimit()
    {
        var started = new DateTimeOffset(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);
        var now = started.AddHours(8).AddSeconds(-1);

        CookieSessionExpiration.IsAbsoluteExpired(now, started, TimeSpan.FromHours(8)).Should().BeFalse();
    }

    [Fact]
    public void IsAbsoluteExpired_IsTrue_AtLimit()
    {
        var started = new DateTimeOffset(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);
        var now = started.AddHours(8);

        CookieSessionExpiration.IsAbsoluteExpired(now, started, TimeSpan.FromHours(8)).Should().BeTrue();
    }

    [Fact]
    public void AdminDefaults_Idle30Minutes_Absolute8Hours()
    {
        var options = new AdminAuthOptions();
        options.GetIdleLifetime().Should().Be(TimeSpan.FromMinutes(30));
        options.GetAbsoluteLifetime().Should().Be(TimeSpan.FromHours(8));
    }

    [Fact]
    public void CustomerDefaults_Session12Hours_Absolute7Days_RememberMe14Days()
    {
        var options = new CustomerAuthOptions();
        options.GetIdleLifetime().Should().Be(TimeSpan.FromHours(12));
        options.GetAbsoluteLifetime().Should().Be(TimeSpan.FromDays(7));
        options.GetRememberMeLifetime().Should().Be(TimeSpan.FromDays(14));
    }

    [Fact]
    public void CustomerRememberMe_IsCappedAt14Days()
    {
        var options = new CustomerAuthOptions { RememberMeDays = 30 };
        options.GetRememberMeLifetime().Should().Be(TimeSpan.FromDays(14));
    }

    [Fact]
    public void CustomerSessionDaysFallback_UsedWhenSessionHoursMissing()
    {
        var options = new CustomerAuthOptions { SessionHours = 0, SessionDays = 30 };
        options.GetIdleLifetime().Should().Be(TimeSpan.FromDays(30));
    }
}
