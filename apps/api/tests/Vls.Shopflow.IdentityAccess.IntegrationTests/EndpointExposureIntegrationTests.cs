using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vls.Shopflow.IdentityAccess.Infrastructure;

namespace Vls.Shopflow.IdentityAccess.IntegrationTests;

public sealed class EndpointExposureIntegrationTests : IClassFixture<ShopflowWebApplicationFactory>
{
    private readonly ShopflowWebApplicationFactory _factory;

    public EndpointExposureIntegrationTests(ShopflowWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task InventoryReserve_WithoutLogin_Returns401()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = _factory.CreateClient();
        var skuId = Guid.NewGuid();

        var response = await client.PostAsJsonAsync(
            $"/api/admin/inventory/skus/{skuId}/reserve",
            new { quantity = 1, expiresAt = (DateTimeOffset?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task InventoryConfirmReservation_WithoutLogin_Returns401()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = _factory.CreateClient();

        var response = await client.PostAsync(
            $"/api/admin/inventory/reservations/{Guid.NewGuid()}/confirm",
            null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task InventoryCancelReservation_WithoutLogin_Returns401()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = _factory.CreateClient();

        var response = await client.PostAsync(
            $"/api/admin/inventory/reservations/{Guid.NewGuid()}/cancel",
            null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LegacyPublicInventoryReservePath_Returns404()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/inventory/skus/{Guid.NewGuid()}/reserve",
            new { quantity = 1, expiresAt = (DateTimeOffset?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task InventoryAdminAddStock_WithoutLogin_Returns401()
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
    public async Task PublicInventoryAvailability_WithoutLogin_ReturnsSafeShapeOrNotFound()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/inventory/skus/{Guid.NewGuid()}");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            body.TryGetProperty("skuId", out _).Should().BeTrue();
            body.TryGetProperty("isAvailable", out _).Should().BeTrue();
            body.TryGetProperty("availableQuantity", out _).Should().BeTrue();
            body.TryGetProperty("quantityOnHand", out _).Should().BeFalse();
            body.TryGetProperty("quantityReserved", out _).Should().BeFalse();
        }
    }

    [Fact]
    public async Task InventorySkuAvailabilityBatch_WithoutLogin_Returns401()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/admin/inventory/skus/availability",
            new { skuIds = new[] { Guid.NewGuid() } });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task InventorySkuAvailabilityBatch_AsCustomer_ReturnsForbiddenOrUnauthorized()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = await _factory.CreateAuthenticatedCustomerClientAsync();

        var response = await client.PostAsJsonAsync(
            "/api/admin/inventory/skus/availability",
            new { skuIds = new[] { Guid.NewGuid() } });

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task InventorySkuAvailabilityBatch_AdminWithoutCsrf_Returns400()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = _factory.CreateAuthenticatedAdminClient();

        var response = await client.PostAsJsonAsync(
            "/api/admin/inventory/skus/availability",
            new { skuIds = new[] { Guid.NewGuid() } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task InventorySkuAvailabilityBatch_AdminWithCsrf_Returns200PreservingOrder()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = _factory.CreateAuthenticatedAdminClient();
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        var token = csrf.GetProperty("token").GetString();

        var skuA = Guid.NewGuid();
        var skuB = Guid.NewGuid();

        var batchRequest = new HttpRequestMessage(HttpMethod.Post, "/api/admin/inventory/skus/availability")
        {
            Content = JsonContent.Create(new { skuIds = new[] { skuA, skuB, skuA } })
        };
        batchRequest.Headers.Add("X-CSRF-TOKEN", token);

        var response = await client.SendAsync(batchRequest);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items");
        items.GetArrayLength().Should().Be(3);

        items[0].GetProperty("skuId").GetGuid().Should().Be(skuA);
        items[1].GetProperty("skuId").GetGuid().Should().Be(skuB);
        items[2].GetProperty("skuId").GetGuid().Should().Be(skuA);

        // Random SKUs have no inventory row — exists=false, no side effects.
        for (var i = 0; i < 3; i++)
        {
            items[i].GetProperty("exists").GetBoolean().Should().BeFalse();
            items[i].GetProperty("availableQuantity").ValueKind.Should().Be(JsonValueKind.Null);
            items[i].GetProperty("quantityOnHand").ValueKind.Should().Be(JsonValueKind.Null);
            items[i].GetProperty("reservedQuantity").ValueKind.Should().Be(JsonValueKind.Null);
        }
    }

    [Fact]
    public async Task InventorySkuAvailabilityBatch_EmptyPayload_Returns400()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = _factory.CreateAuthenticatedAdminClient();
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        var token = csrf.GetProperty("token").GetString();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/inventory/skus/availability")
        {
            Content = JsonContent.Create(new { skuIds = Array.Empty<Guid>() })
        };
        request.Headers.Add("X-CSRF-TOKEN", token);

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task OrderById_WithoutLogin_Returns401()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/orders/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminOrdersList_WithoutLogin_Returns401()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/admin/orders");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminOrdersDetail_WithoutLogin_Returns401()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/admin/orders/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminOrdersList_AsCustomer_ReturnsForbiddenOrUnauthorized()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = await _factory.CreateAuthenticatedCustomerClientAsync();

        var response = await client.GetAsync("/api/admin/orders");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminOrdersList_AsAdmin_Returns200()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = _factory.CreateAuthenticatedAdminClient();

        var response = await client.GetAsync("/api/admin/orders?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CustomerOrdersList_WithoutLogin_Returns401()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/customer/orders");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CustomerOrdersDetail_WithoutLogin_Returns401()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/customer/orders/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CustomerOrdersList_AsAdmin_ReturnsForbiddenOrUnauthorized()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = _factory.CreateAuthenticatedAdminClient();

        var response = await client.GetAsync("/api/customer/orders");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CustomerOrdersList_AsCustomer_Returns200()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = await _factory.CreateAuthenticatedCustomerClientAsync();

        var response = await client.GetAsync("/api/customer/orders?page=1&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PixPaymentById_WithoutLogin_Returns401()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/payments/pix/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PublicCatalogCategories_WithoutLogin_Returns200()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/catalog/categories");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Health_WithoutLogin_Returns200WithOkStatus()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("ok");
        body.GetProperty("environment").GetString().Should().NotBeNullOrWhiteSpace();
        body.TryGetProperty("connectionString", out _).Should().BeFalse();
        body.TryGetProperty("password", out _).Should().BeFalse();
    }
}
