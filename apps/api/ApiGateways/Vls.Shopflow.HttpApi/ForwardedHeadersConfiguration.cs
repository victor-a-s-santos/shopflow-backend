using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;

namespace Vls.Shopflow.HttpApi;

/// <summary>
/// Restricts <see cref="ForwardedHeadersOptions"/> so X-Forwarded-* is only honored
/// from reverse proxies on private Docker / loopback networks (Caddy), never from
/// arbitrary internet clients.
/// </summary>
public static class ForwardedHeadersConfiguration
{
    /// <summary>
    /// CIDRs for the Docker Compose network and local loopback where Caddy reaches the API.
    /// Cloudflare talks to Caddy, not to the API; CF IP ranges must be configured as Caddy
    /// <c>trusted_proxies</c>, not as ASP.NET KnownIPNetworks (the API's TCP peer is Caddy).
    /// </summary>
    public static readonly string[] TrustedProxyCidrs =
    [
        "10.0.0.0/8",
        "172.16.0.0/12",
        "192.168.0.0/16",
        "127.0.0.0/8",
        "fc00::/7",
        "::1/128"
    ];

    public static void Configure(ForwardedHeadersOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                                   | ForwardedHeaders.XForwardedProto
                                   | ForwardedHeaders.XForwardedHost;

        // Single hop: client → (Cloudflare) → Caddy → API.
        options.ForwardLimit = 1;

        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();

        foreach (var cidr in TrustedProxyCidrs)
            options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(cidr));
    }
}
