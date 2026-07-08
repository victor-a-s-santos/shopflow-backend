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
    public async Task OrderById_WithoutLogin_Returns401()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/orders/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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
