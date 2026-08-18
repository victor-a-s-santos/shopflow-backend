using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Vls.Shopflow.HttpApi;

namespace Vls.Shopflow.IdentityAccess.IntegrationTests;

public sealed class ForwardedHeadersConfigurationTests
{
    [Fact]
    public void Configure_TrustsOnlyPrivateAndLoopbackNetworks()
    {
        var options = new ForwardedHeadersOptions();
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();

        ForwardedHeadersConfiguration.Configure(options);

        options.KnownProxies.Should().BeEmpty();
        options.KnownIPNetworks.Should().HaveCount(ForwardedHeadersConfiguration.TrustedProxyCidrs.Length);
        options.ForwardLimit.Should().Be(1);
        options.ForwardedHeaders.Should().HaveFlag(ForwardedHeaders.XForwardedProto);

        // Public internet addresses must not match trusted proxy networks.
        options.KnownIPNetworks.Any(n => n.Contains(IPAddress.Parse("8.8.8.8"))).Should().BeFalse();
        // Typical Docker Compose bridge / overlay addresses must match.
        options.KnownIPNetworks.Any(n => n.Contains(IPAddress.Parse("172.18.0.5"))).Should().BeTrue();
        options.KnownIPNetworks.Any(n => n.Contains(IPAddress.Parse("10.0.0.2"))).Should().BeTrue();
        options.KnownIPNetworks.Any(n => n.Contains(IPAddress.Parse("127.0.0.1"))).Should().BeTrue();
    }
}
