using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vls.Shopflow.IdentityAccess.Domain.Constants;
using Vls.Shopflow.IdentityAccess.Domain.Enums;
using Vls.Shopflow.IdentityAccess.Infrastructure.Identity;

namespace Vls.Shopflow.IdentityAccess.IntegrationTests;

public sealed class PrivateStoreAccessWebApplicationFactory : ShopflowWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["StoreAccess:Mode"] = "PrivateCatalogApprovedOnly",
                ["Checkout:AllowGuestCheckout"] = "false",
                ["CustomerAccess:RequireApproval"] = "true"
            });
        });
    }
}

public sealed class CustomerApprovalIntegrationTests : IClassFixture<PrivateStoreAccessWebApplicationFactory>
{
    private readonly PrivateStoreAccessWebApplicationFactory _factory;

    public CustomerApprovalIntegrationTests(PrivateStoreAccessWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task StoreAccess_ReturnsPrivatePolicy()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/store/access");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("mode").GetString().Should().Be("PrivateCatalogApprovedOnly");
        body.GetProperty("allowGuestCheckout").GetBoolean().Should().BeFalse();
        body.GetProperty("requireApprovedCustomerToBrowse").GetBoolean().Should().BeTrue();
        body.GetProperty("requireApprovedCustomerForCheckout").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Register_CreatesPendingApprovalAndDoesNotLogin()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = _factory.CreateClient();
        var email = $"pending-{Guid.NewGuid():N}@test.local";

        var response = await client.PostAsJsonAsync("/api/auth/customer/register", new
        {
            email,
            password = ShopflowWebApplicationFactory.CustomerPassword,
            fullName = "Pending Customer",
            phone = "11988887777"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("accessStatus").GetString().Should().Be(nameof(CustomerAccessStatus.PendingApproval));
        body.GetProperty("approvedAt").ValueKind.Should().Be(JsonValueKind.Null);

        var me = await client.GetAsync("/api/auth/customer/me");
        me.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LoginAndMe_ReturnPendingStatus()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var email = await _factory.RegisterCustomerAsync();
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });

        var login = await client.PostAsJsonAsync("/api/auth/customer/login", new
        {
            email,
            password = ShopflowWebApplicationFactory.CustomerPassword
        });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginBody = await login.Content.ReadFromJsonAsync<JsonElement>();
        loginBody.GetProperty("accessStatus").GetString().Should().Be(nameof(CustomerAccessStatus.PendingApproval));

        var me = await client.GetAsync("/api/auth/customer/me");
        me.StatusCode.Should().Be(HttpStatusCode.OK);
        var meBody = await me.Content.ReadFromJsonAsync<JsonElement>();
        meBody.GetProperty("accessStatus").GetString().Should().Be(nameof(CustomerAccessStatus.PendingApproval));
    }

    [Fact]
    public async Task Catalog_WithoutLogin_Returns401()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/catalog/products");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("code").GetString().Should().Be(StoreAccessErrorCodes.CustomerLoginRequired);
    }

    [Fact]
    public async Task CatalogAndCheckout_PendingCustomer_Returns403()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = await CreatePendingCustomerClientAsync();

        var catalog = await client.GetAsync("/api/catalog/categories");
        catalog.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var catalogBody = await catalog.Content.ReadFromJsonAsync<JsonElement>();
        catalogBody.GetProperty("code").GetString().Should().Be(StoreAccessErrorCodes.CustomerAccessNotApproved);

        var checkout = await client.PostAsJsonAsync("/api/checkout/sessions", SampleCheckoutRequest());
        checkout.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var checkoutBody = await checkout.Content.ReadFromJsonAsync<JsonElement>();
        checkoutBody.GetProperty("code").GetString().Should().Be(StoreAccessErrorCodes.CustomerAccessNotApproved);
    }

    [Fact]
    public async Task GuestCheckout_WithoutLogin_Returns401GuestDisabled()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/checkout/sessions", SampleCheckoutRequest());
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("code").GetString().Should().Be(StoreAccessErrorCodes.GuestCheckoutDisabled);

        var order = await client.PostAsJsonAsync("/api/orders/from-checkout-session", new
        {
            checkoutSessionId = Guid.NewGuid()
        });
        order.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var orderBody = await order.Content.ReadFromJsonAsync<JsonElement>();
        orderBody.GetProperty("code").GetString().Should().Be(StoreAccessErrorCodes.GuestCheckoutDisabled);
    }

    [Fact]
    public async Task Admin_CanListApproveRejectSuspendAndReactivate()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var pendingEmail = await _factory.RegisterCustomerAsync();
        var admin = _factory.CreateAuthenticatedAdminClient();
        var csrf = await admin.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        var token = csrf.GetProperty("token").GetString();

        var count = await admin.GetFromJsonAsync<JsonElement>("/api/admin/customers/pending-count");
        count.GetProperty("pendingCount").GetInt32().Should().BeGreaterThan(0);

        var list = await admin.GetAsync("/api/admin/customers?status=PendingApproval&q=" + Uri.EscapeDataString(pendingEmail));
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var listBody = await list.Content.ReadFromJsonAsync<JsonElement>();
        var items = listBody.GetProperty("items");
        items.GetArrayLength().Should().BeGreaterThan(0);
        var customerId = items[0].GetProperty("customerId").GetGuid();
        items[0].GetProperty("accessStatus").GetString().Should().Be(nameof(CustomerAccessStatus.PendingApproval));

        var approveRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/customers/{customerId}/approve")
        {
            Content = JsonContent.Create(new { reason = "lojista conhecido" })
        };
        approveRequest.Headers.Add("X-CSRF-TOKEN", token);
        var approve = await admin.SendAsync(approveRequest);
        approve.StatusCode.Should().Be(HttpStatusCode.OK);
        var approvedBody = await approve.Content.ReadFromJsonAsync<JsonElement>();
        approvedBody.GetProperty("accessStatus").GetString().Should().Be(nameof(CustomerAccessStatus.Approved));
        approvedBody.GetProperty("accessDecidedByAdminUserId").GetGuid().Should().NotBeEmpty();

        var suspendRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/customers/{customerId}/suspend")
        {
            Content = JsonContent.Create(new { reason = "inadimplencia" })
        };
        suspendRequest.Headers.Add("X-CSRF-TOKEN", token);
        var suspend = await admin.SendAsync(suspendRequest);
        suspend.StatusCode.Should().Be(HttpStatusCode.OK);
        (await suspend.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessStatus").GetString().Should().Be(nameof(CustomerAccessStatus.Suspended));

        var reactivateRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/customers/{customerId}/reactivate")
        {
            Content = JsonContent.Create(new { reason = "regularizado" })
        };
        reactivateRequest.Headers.Add("X-CSRF-TOKEN", token);
        var reactivate = await admin.SendAsync(reactivateRequest);
        reactivate.StatusCode.Should().Be(HttpStatusCode.OK);
        (await reactivate.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessStatus").GetString().Should().Be(nameof(CustomerAccessStatus.Approved));

        var otherEmail = await _factory.RegisterCustomerAsync();
        Guid otherId;
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ShopflowUser>>();
            var other = await users.FindByEmailAsync(otherEmail);
            otherId = other!.Id;
        }

        var rejectRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/customers/{otherId}/reject")
        {
            Content = JsonContent.Create(new { reason = "fora do perfil" })
        };
        rejectRequest.Headers.Add("X-CSRF-TOKEN", token);
        var reject = await admin.SendAsync(rejectRequest);
        reject.StatusCode.Should().Be(HttpStatusCode.OK);
        (await reject.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessStatus").GetString().Should().Be(nameof(CustomerAccessStatus.Rejected));
    }

    [Fact]
    public async Task AdminCustomers_AsCustomer_IsForbidden()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = await CreatePendingCustomerClientAsync();
        var response = await client.GetAsync("/api/admin/customers");
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ApprovedCustomer_CanReadCatalog()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var email = await _factory.RegisterCustomerAsync();
        await ApproveCustomerAsync(email);

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });
        var login = await client.PostAsJsonAsync("/api/auth/customer/login", new
        {
            email,
            password = ShopflowWebApplicationFactory.CustomerPassword
        });
        login.EnsureSuccessStatusCode();

        var catalog = await client.GetAsync("/api/catalog/categories");
        catalog.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RejectedAndSuspended_CannotCheckout()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var rejectedEmail = await _factory.RegisterCustomerAsync();
        await SetStatusAsync(rejectedEmail, CustomerAccessStatus.Rejected);
        var rejectedClient = await LoginCustomerAsync(rejectedEmail);
        var rejectedCheckout = await rejectedClient.PostAsJsonAsync("/api/checkout/sessions", SampleCheckoutRequest());
        rejectedCheckout.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await rejectedCheckout.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("code").GetString().Should().Be(StoreAccessErrorCodes.CustomerAccessRejected);

        var suspendedEmail = await _factory.RegisterCustomerAsync();
        await ApproveCustomerAsync(suspendedEmail);
        await SetStatusAsync(suspendedEmail, CustomerAccessStatus.Suspended);
        var suspendedClient = await LoginCustomerAsync(suspendedEmail);
        var suspendedCheckout = await suspendedClient.PostAsJsonAsync("/api/checkout/sessions", SampleCheckoutRequest());
        suspendedCheckout.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await suspendedCheckout.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("code").GetString().Should().Be(StoreAccessErrorCodes.CustomerAccessSuspended);
    }

    private async Task<HttpClient> CreatePendingCustomerClientAsync()
    {
        var email = await _factory.RegisterCustomerAsync();
        return await LoginCustomerAsync(email);
    }

    private async Task<HttpClient> LoginCustomerAsync(string email)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });
        var login = await client.PostAsJsonAsync("/api/auth/customer/login", new
        {
            email,
            password = ShopflowWebApplicationFactory.CustomerPassword
        });
        login.EnsureSuccessStatusCode();
        return client;
    }

    private async Task ApproveCustomerAsync(string email)
        => await SetStatusAsync(email, CustomerAccessStatus.Approved);

    private async Task SetStatusAsync(string email, CustomerAccessStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ShopflowUser>>();
        var user = await users.FindByEmailAsync(email);
        user.Should().NotBeNull();
        user!.AccessStatus = status;
        user.ApprovedAt = status == CustomerAccessStatus.Approved ? DateTimeOffset.UtcNow : null;
        (await users.UpdateAsync(user)).Succeeded.Should().BeTrue();
    }

    private static object SampleCheckoutRequest()
        => new
        {
            customer = new { fullName = "Guest", email = "guest@test.local", phone = "11999999999" },
            address = new
            {
                zipCode = "01001000",
                street = "Rua Teste",
                number = "1",
                complement = (string?)null,
                neighborhood = "Centro",
                city = "São Paulo",
                state = "SP"
            },
            items = new[] { new { skuId = Guid.NewGuid(), quantity = 1 } }
        };
}
