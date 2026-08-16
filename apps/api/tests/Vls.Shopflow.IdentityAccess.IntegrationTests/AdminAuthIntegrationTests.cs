using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vls.Shopflow.IdentityAccess.Application.Interfaces;
using Vls.Shopflow.IdentityAccess.Infrastructure;
using Vls.Shopflow.IdentityAccess.IntegrationTests.Support;

namespace Vls.Shopflow.IdentityAccess.IntegrationTests;

public sealed class AdminAuthIntegrationTests : IClassFixture<ShopflowWebApplicationFactory>
{
    private readonly ShopflowWebApplicationFactory _factory;

    public AdminAuthIntegrationTests(ShopflowWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AdminLogin_WithValidCredentials_ReturnsUserAndSetsCookie()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });

        var response = await client.PostAsJsonAsync("/api/auth/admin/login", new
        {
            email = ShopflowWebApplicationFactory.AdminEmail,
            password = ShopflowWebApplicationFactory.AdminPassword
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        cookies!.Should().Contain(c => c.Contains("shopflow_admin", StringComparison.OrdinalIgnoreCase));

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("email").GetString().Should().Be(ShopflowWebApplicationFactory.AdminEmail);
        body.GetProperty("roles").EnumerateArray().Should().Contain(r => r.GetString() == "Owner");
    }

    [Fact]
    public async Task AdminLogin_WithInvalidCredentials_Returns401WithGenericMessage()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/admin/login", new
        {
            email = ShopflowWebApplicationFactory.AdminEmail,
            password = "wrong-password-123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("message").GetString().Should().Be("Invalid email or password.");
    }

    [Fact]
    public async Task AdminMe_WithoutLogin_Returns401()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/auth/admin/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminMe_WithLogin_ReturnsUserData()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = _factory.CreateAuthenticatedAdminClient();

        var response = await client.GetAsync("/api/auth/admin/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("email").GetString().Should().Be(ShopflowWebApplicationFactory.AdminEmail);
    }

    [Fact]
    public async Task AdminCatalogEndpoint_WithoutLogin_Returns401()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/catalog/products/variant", new
        {
            name = "Blocked Product",
            slug = $"blocked-{Guid.NewGuid():N}",
            categoryId = (Guid?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminLogout_WithoutCsrf_Returns400()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = _factory.CreateAuthenticatedAdminClient();

        var response = await client.PostAsync("/api/auth/admin/logout", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AdminLogout_WithCsrf_Returns204()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = _factory.CreateAuthenticatedAdminClient();
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        var token = csrf.GetProperty("token").GetString();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/admin/logout");
        request.Headers.Add("X-CSRF-TOKEN", token);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var meResponse = await client.GetAsync("/api/auth/admin/me");
        meResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminInventoryAddStock_WithoutLogin_Returns401()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/inventory/skus/{Guid.NewGuid()}/add",
            new { quantity = 1, reason = "test" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PublicCatalogEndpoint_WithoutLogin_Returns200()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/catalog/categories");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

public class ShopflowWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin.integration@test.local";
    public const string AdminPassword = "TestAdmin123";
    public const string CustomerPassword = "CustomerPass123";

    public CapturingIdentityEmailSender EmailSender { get; } = new();

    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("SHOPFLOW_TEST_DB")
        ?? "Host=localhost;Port=5432;Database=shopflow;Username=postgres;Password=postgres";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Catalog"] = ConnectionString,
                ["ConnectionStrings:Inventory"] = ConnectionString,
                ["ConnectionStrings:CartCheckout"] = ConnectionString,
                ["ConnectionStrings:Orders"] = ConnectionString,
                ["ConnectionStrings:PaymentsPix"] = ConnectionString,
                ["ConnectionStrings:IdentityAccess"] = ConnectionString,
                ["ConnectionStrings:Notifications"] = ConnectionString,
                ["SHOPFLOW_ADMIN_EMAIL"] = AdminEmail,
                ["SHOPFLOW_ADMIN_PASSWORD"] = AdminPassword,
                ["SHOPFLOW_ADMIN_NAME"] = "Integration Admin",
                ["DataProtection:KeysPath"] = Path.Combine(Path.GetTempPath(), "shopflow-test-dataprotection"),
                ["AllowedOrigins:0"] = "http://localhost",
                ["StoreAccess:Mode"] = "PublicCatalogAndGuestCheckout",
                ["Checkout:AllowGuestCheckout"] = "true",
                ["CustomerAccess:RequireApproval"] = "false"
            });
        });

        builder.ConfigureServices(services =>
        {
            foreach (var descriptor in services.Where(d => d.ServiceType == typeof(IIdentityEmailSender)).ToList())
                services.Remove(descriptor);

            services.AddSingleton<IIdentityEmailSender>(EmailSender);
        });
    }

    public async Task<bool> CanConnectToDatabaseAsync()
    {
        try
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IdentityAccessDbContext>();
            return await db.Database.CanConnectAsync();
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> RegisterCustomerAsync(string? email = null)
    {
        email ??= $"customer-{Guid.NewGuid():N}@test.local";
        EmailSender.Clear();

        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/customer/register", new
        {
            email,
            password = CustomerPassword,
            fullName = "Integration Customer",
            phone = "11988887777"
        });
        response.EnsureSuccessStatusCode();
        return email;
    }

    public async Task<HttpClient> CreateAuthenticatedCustomerClientAsync()
    {
        var email = await RegisterCustomerAsync();
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });

        var loginResponse = await client.PostAsJsonAsync("/api/auth/customer/login", new
        {
            email,
            password = CustomerPassword
        });
        loginResponse.EnsureSuccessStatusCode();
        return client;
    }

    public HttpClient CreateAuthenticatedAdminClient()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });

        var loginResponse = client.PostAsJsonAsync("/api/auth/admin/login", new
        {
            email = AdminEmail,
            password = AdminPassword
        }).GetAwaiter().GetResult();

        loginResponse.EnsureSuccessStatusCode();
        return client;
    }
}
