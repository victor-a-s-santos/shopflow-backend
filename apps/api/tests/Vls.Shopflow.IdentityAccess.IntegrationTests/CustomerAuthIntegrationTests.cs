using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vls.Shopflow.IdentityAccess.Application.Interfaces;
using Vls.Shopflow.IdentityAccess.Domain.Constants;
using Vls.Shopflow.IdentityAccess.Infrastructure;
using Vls.Shopflow.IdentityAccess.Infrastructure.Identity;
using Vls.Shopflow.IdentityAccess.IntegrationTests.Support;

namespace Vls.Shopflow.IdentityAccess.IntegrationTests;

public sealed class CustomerAuthIntegrationTests : IClassFixture<ShopflowWebApplicationFactory>
{
    private readonly ShopflowWebApplicationFactory _factory;

    public CustomerAuthIntegrationTests(ShopflowWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RegisterCustomer_ValidRequest_Returns201()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = _factory.CreateClient();
        var email = $"customer-{Guid.NewGuid():N}@test.local";

        var response = await client.PostAsJsonAsync("/api/auth/customer/register", new
        {
            email,
            password = "CustomerPass123",
            fullName = "Test Customer",
            phone = "11999999999"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("email").GetString().Should().Be(email);
        body.GetProperty("emailConfirmed").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task RegisterCustomer_DuplicateEmail_Returns409()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = _factory.CreateClient();
        var email = $"dup-{Guid.NewGuid():N}@test.local";
        var payload = new
        {
            email,
            password = "CustomerPass123",
            fullName = "Dup Customer",
            phone = "11999999999"
        };

        (await client.PostAsJsonAsync("/api/auth/customer/register", payload)).EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync("/api/auth/customer/register", payload);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task LoginCustomer_ValidCredentials_Returns200AndCustomerCookie()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var email = await _factory.RegisterCustomerAsync();
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });

        var response = await client.PostAsJsonAsync("/api/auth/customer/login", new
        {
            email,
            password = ShopflowWebApplicationFactory.CustomerPassword
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        cookies!.Should().Contain(c => c.Contains("shopflow_customer", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task LoginCustomer_InvalidCredentials_Returns401Generic()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/customer/login", new
        {
            email = "nobody@test.local",
            password = "WrongPass123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("message").GetString().Should().Be("Invalid email or password.");
    }

    [Fact]
    public async Task CustomerMe_WithoutLogin_Returns401()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/auth/customer/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CustomerMe_WithCustomerCookie_Returns200()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = await _factory.CreateAuthenticatedCustomerClientAsync();
        var response = await client.GetAsync("/api/auth/customer/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("roles").EnumerateArray().Should().Contain(r => r.GetString() == AuthRoles.Customer);
    }

    [Fact]
    public async Task ForgotPassword_AlwaysReturnsGenericMessage()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/customer/forgot-password", new
        {
            email = "unknown@test.local"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("message").GetString().Should()
            .Be("If the email is registered, we will send password reset instructions.");
    }

    [Fact]
    public async Task ResetPassword_WithValidToken_Succeeds()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var email = await _factory.RegisterCustomerAsync();
        var client = _factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/customer/forgot-password", new { email });
        var token = _factory.EmailSender.GetResetToken(email);
        token.Should().NotBeNullOrEmpty();

        var response = await client.PostAsJsonAsync("/api/auth/customer/reset-password", new
        {
            email,
            token,
            newPassword = "NewCustomerPass456"
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/customer/login", new
        {
            email,
            password = "NewCustomerPass456"
        });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ConfirmEmail_WithValidToken_Succeeds()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var email = await _factory.RegisterCustomerAsync();
        var client = _factory.CreateClient();

        var token = _factory.EmailSender.GetConfirmToken(email);
        token.Should().NotBeNullOrEmpty();

        var response = await client.PostAsJsonAsync("/api/auth/customer/confirm-email", new
        {
            email,
            token
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ShopflowUser>>();
        var user = await userManager.FindByEmailAsync(email);
        user!.EmailConfirmed.Should().BeTrue();
    }
}

public sealed class CustomerAdminSeparationIntegrationTests : IClassFixture<ShopflowWebApplicationFactory>
{
    private readonly ShopflowWebApplicationFactory _factory;

    public CustomerAdminSeparationIntegrationTests(ShopflowWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CustomerLoggedIn_CannotAccessAdminMe()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = await _factory.CreateAuthenticatedCustomerClientAsync();
        var response = await client.GetAsync("/api/auth/admin/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminLoggedIn_CannotAccessCustomerMe()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = _factory.CreateAuthenticatedAdminClient();
        var response = await client.GetAsync("/api/auth/customer/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CustomerLoggedIn_CannotAccessCatalogAdminEndpoint()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = await _factory.CreateAuthenticatedCustomerClientAsync();
        var response = await client.PostAsJsonAsync("/api/catalog/products/variant", new
        {
            name = "Blocked",
            slug = $"blocked-{Guid.NewGuid():N}",
            categoryId = (Guid?)null
        });

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PublicCatalog_RemainsAccessibleWithoutCustomerCookie()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/catalog/categories");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CustomerLogout_WithoutCsrf_Returns400()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = await _factory.CreateAuthenticatedCustomerClientAsync();
        var response = await client.PostAsync("/api/auth/customer/logout", null);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CustomerLogout_WithCsrf_Returns204()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = await _factory.CreateAuthenticatedCustomerClientAsync();
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        var token = csrf.GetProperty("token").GetString();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/customer/logout");
        request.Headers.Add("X-CSRF-TOKEN", token);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var meResponse = await client.GetAsync("/api/auth/customer/me");
        meResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
