using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Vls.Shopflow.IdentityAccess.Application.Interfaces;

namespace Vls.Shopflow.IdentityAccess.IntegrationTests;

public sealed class CustomerForgotPasswordEnqueueFailureTests : IClassFixture<ForgotPasswordEnqueueFailureFactory>
{
    private readonly ForgotPasswordEnqueueFailureFactory _factory;

    public CustomerForgotPasswordEnqueueFailureTests(ForgotPasswordEnqueueFailureFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ForgotPassword_WhenEnqueueFails_StillReturnsGenericMessage()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var email = await _factory.RegisterCustomerAsync();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/customer/forgot-password", new { email });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("message").GetString().Should()
            .Be("If the email is registered, we will send password reset instructions.");
    }
}

public sealed class ForgotPasswordEnqueueFailureFactory : ShopflowWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            foreach (var descriptor in services.Where(d => d.ServiceType == typeof(IIdentityEmailSender)).ToList())
                services.Remove(descriptor);

            services.AddSingleton<IIdentityEmailSender, ThrowingOnResetIdentityEmailSender>();
        });
    }
}

public sealed class ThrowingOnResetIdentityEmailSender : IIdentityEmailSender
{
    public Task SendEmailConfirmationAsync(string email, string token, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SendPasswordResetAsync(string email, string token, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("outbox down");
}
