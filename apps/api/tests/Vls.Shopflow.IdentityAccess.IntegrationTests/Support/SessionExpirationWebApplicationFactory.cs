using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Vls.Shopflow.IdentityAccess.IntegrationTests;

namespace Vls.Shopflow.IdentityAccess.IntegrationTests.Support;

public sealed class SessionExpirationWebApplicationFactory : ShopflowWebApplicationFactory
{
    public static readonly DateTimeOffset StartUtc = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);

    public FakeTimeProvider Time { get; } = new(StartUtc);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            foreach (var descriptor in services.Where(d => d.ServiceType == typeof(TimeProvider)).ToList())
                services.Remove(descriptor);

            services.AddSingleton<TimeProvider>(Time);
        });
    }
}
