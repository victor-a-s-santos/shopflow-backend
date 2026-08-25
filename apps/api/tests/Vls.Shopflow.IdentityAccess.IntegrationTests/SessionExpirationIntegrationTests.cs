using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Vls.Shopflow.IdentityAccess.IntegrationTests.Support;

namespace Vls.Shopflow.IdentityAccess.IntegrationTests;

public sealed class SessionExpirationIntegrationTests : IClassFixture<SessionExpirationWebApplicationFactory>
{
    private readonly SessionExpirationWebApplicationFactory _factory;

    public SessionExpirationIntegrationTests(SessionExpirationWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Admin_LoginValid_Returns200AndCookie()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = CreateClient();
        var response = await LoginAdminAsync(client);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        CookieHeader(response).Should().Contain(c => c.Contains("shopflow_admin", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Admin_WithinIdleWindow_RemainsAuthenticated()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = CreateClient();
        (await LoginAdminAsync(client)).EnsureSuccessStatusCode();

        _factory.Time.Advance(TimeSpan.FromMinutes(10));

        var me = await client.GetAsync("/api/auth/admin/me");
        me.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Admin_AfterIdleTimeout_Returns401()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = CreateClient();
        (await LoginAdminAsync(client)).EnsureSuccessStatusCode();

        _factory.Time.Advance(TimeSpan.FromMinutes(31));

        var me = await client.GetAsync("/api/auth/admin/me");
        me.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Admin_SlidingRenews_BeforeIdleLimit()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = CreateClient();
        (await LoginAdminAsync(client)).EnsureSuccessStatusCode();

        _factory.Time.Advance(TimeSpan.FromMinutes(16));
        (await client.GetAsync("/api/auth/admin/me")).StatusCode.Should().Be(HttpStatusCode.OK);

        _factory.Time.Advance(TimeSpan.FromMinutes(16));
        (await client.GetAsync("/api/auth/admin/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Admin_AfterAbsoluteTimeout_Returns401EvenWithActivity()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = CreateClient();
        (await LoginAdminAsync(client)).EnsureSuccessStatusCode();

        for (var elapsed = TimeSpan.Zero; elapsed < TimeSpan.FromHours(8); elapsed += TimeSpan.FromMinutes(10))
        {
            _factory.Time.Advance(TimeSpan.FromMinutes(10));
            var probe = await client.GetAsync("/api/auth/admin/me");
            if (elapsed + TimeSpan.FromMinutes(10) >= TimeSpan.FromHours(8))
            {
                probe.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
                return;
            }

            probe.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task Admin_Logout_InvalidatesSession()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = CreateClient();
        (await LoginAdminAsync(client)).EnsureSuccessStatusCode();

        var csrf = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/auth/csrf");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/admin/logout");
        request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        (await client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.GetAsync("/api/auth/admin/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Customer_LoginValid_Returns200AndCookie()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = CreateClient();
        var email = await _factory.RegisterCustomerAsync();
        var response = await LoginCustomerAsync(client, email);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        CookieHeader(response).Should().Contain(c => c.Contains("shopflow_customer", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Customer_AfterIdleTimeout_Returns401()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = CreateClient();
        var email = await _factory.RegisterCustomerAsync();
        (await LoginCustomerAsync(client, email)).EnsureSuccessStatusCode();

        _factory.Time.Advance(TimeSpan.FromHours(12).Add(TimeSpan.FromMinutes(1)));

        (await client.GetAsync("/api/auth/customer/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Customer_RememberMe_PersistsCookieAndRespects14DayCap()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = CreateClient();
        var email = await _factory.RegisterCustomerAsync();
        var response = await LoginCustomerAsync(client, email, rememberMe: true);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        CookieHeader(response).Should().Contain(c =>
            c.Contains("shopflow_customer", StringComparison.OrdinalIgnoreCase)
            && c.Contains("expires=", StringComparison.OrdinalIgnoreCase));

        _factory.Time.Advance(TimeSpan.FromDays(13));
        (await client.GetAsync("/api/auth/customer/me")).StatusCode.Should().Be(HttpStatusCode.OK);

        _factory.Time.Advance(TimeSpan.FromDays(1).Add(TimeSpan.FromMinutes(1)));
        (await client.GetAsync("/api/auth/customer/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Customer_AbsoluteTimeout_PreventsInfiniteSession()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = CreateClient();
        var email = await _factory.RegisterCustomerAsync();
        (await LoginCustomerAsync(client, email)).EnsureSuccessStatusCode();

        for (var elapsed = TimeSpan.Zero; elapsed < TimeSpan.FromDays(7); elapsed += TimeSpan.FromHours(6))
        {
            _factory.Time.Advance(TimeSpan.FromHours(6));
            var probe = await client.GetAsync("/api/auth/customer/me");
            if (elapsed + TimeSpan.FromHours(6) >= TimeSpan.FromDays(7))
            {
                probe.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
                return;
            }

            probe.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task Customer_ExpiredCookie_MeReturns401()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = CreateClient();
        var email = await _factory.RegisterCustomerAsync();
        (await LoginCustomerAsync(client, email)).EnsureSuccessStatusCode();
        (await client.GetAsync("/api/auth/customer/me")).StatusCode.Should().Be(HttpStatusCode.OK);

        _factory.Time.Advance(TimeSpan.FromHours(13));
        (await client.GetAsync("/api/auth/customer/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CustomerCookie_DoesNotAuthenticateAdminMe()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = CreateClient();
        var email = await _factory.RegisterCustomerAsync();
        (await LoginCustomerAsync(client, email)).EnsureSuccessStatusCode();

        (await client.GetAsync("/api/auth/admin/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminCookie_DoesNotAuthenticateCustomerMe()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = CreateClient();
        (await LoginAdminAsync(client)).EnsureSuccessStatusCode();

        (await client.GetAsync("/api/auth/customer/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private HttpClient CreateClient()
        => _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });

    private static Task<HttpResponseMessage> LoginAdminAsync(HttpClient client)
        => client.PostAsJsonAsync("/api/auth/admin/login", new
        {
            email = ShopflowWebApplicationFactory.AdminEmail,
            password = ShopflowWebApplicationFactory.AdminPassword
        });

    private static Task<HttpResponseMessage> LoginCustomerAsync(
        HttpClient client,
        string email,
        bool rememberMe = false)
        => client.PostAsJsonAsync("/api/auth/customer/login", new
        {
            email,
            password = ShopflowWebApplicationFactory.CustomerPassword,
            rememberMe
        });

    private static IEnumerable<string> CookieHeader(HttpResponseMessage response)
    {
        response.Headers.TryGetValues("Set-Cookie", out var cookies);
        return cookies ?? [];
    }
}
