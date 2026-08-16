using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Vls.Shopflow.IdentityAccess.Application.Interfaces;

namespace Vls.Shopflow.IdentityAccess.IntegrationTests;

public sealed class CustomerApprovalEmailFailureTests : IClassFixture<ThrowingCustomerAccessNotifierFactory>
{
    private readonly ThrowingCustomerAccessNotifierFactory _factory;

    public CustomerApprovalEmailFailureTests(ThrowingCustomerAccessNotifierFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_WhenApprovalEmailEnqueueFails_StillCreatesPending()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = _factory.CreateClient();
        var email = $"enqueue-fail-{Guid.NewGuid():N}@test.local";
        var response = await client.PostAsJsonAsync("/api/auth/customer/register", new
        {
            email,
            password = ShopflowWebApplicationFactory.CustomerPassword,
            fullName = "Enqueue Fail",
            phone = "11988887777"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("approvalStatus").GetString().Should().Be("Pending");
        body.GetProperty("message").GetString().Should().Be("Cadastro enviado para aprovação.");
    }
}

public sealed class ThrowingCustomerAccessNotifierFactory : PrivateStoreAccessWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            foreach (var descriptor in services.Where(d =>
                         d.ServiceType == typeof(ICustomerAccessNotifier)
                         || d.ServiceType == typeof(ICustomerPendingApprovalNotifier)).ToList())
            {
                services.Remove(descriptor);
            }

            services.AddSingleton<ThrowingCustomerAccessNotifier>();
            services.AddSingleton<ICustomerAccessNotifier>(sp =>
                sp.GetRequiredService<ThrowingCustomerAccessNotifier>());
            services.AddSingleton<ICustomerPendingApprovalNotifier>(sp =>
                sp.GetRequiredService<ThrowingCustomerAccessNotifier>());
        });
    }
}

public sealed class ThrowingCustomerAccessNotifier : ICustomerAccessNotifier, ICustomerPendingApprovalNotifier
{
    public Task NotifyRegisteredPendingAsync(
        CustomerRegisteredPendingApproval notification,
        CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("outbox down");

    public Task NotifyApprovedAsync(
        CustomerAccessChangedNotification notification,
        CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("outbox down");

    public Task NotifyRejectedAsync(
        CustomerAccessChangedNotification notification,
        CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("outbox down");

    public Task NotifySuspendedAsync(
        CustomerAccessChangedNotification notification,
        CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("outbox down");
}
