using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace Vls.Shopflow.IdentityAccess.IntegrationTests;

public sealed class CustomerAddressIntegrationTests : IClassFixture<ShopflowWebApplicationFactory>
{
    private readonly ShopflowWebApplicationFactory _factory;

    public CustomerAddressIntegrationTests(ShopflowWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task FirstAddress_BecomesDefault_SecondKeepsFirst_UntilMarked()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = await _factory.CreateAuthenticatedCustomerClientAsync();
        await AddCsrfAsync(client);

        var first = await client.PostAsJsonAsync("/api/customer/addresses", AddressPayload("Casa", "01310100"));
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>();
        firstBody.GetProperty("isDefault").GetBoolean().Should().BeTrue();
        var firstId = firstBody.GetProperty("id").GetGuid();

        var second = await client.PostAsJsonAsync("/api/customer/addresses", AddressPayload("Loja", "01001000"));
        second.EnsureSuccessStatusCode();
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>();
        secondBody.GetProperty("isDefault").GetBoolean().Should().BeFalse();
        var secondId = secondBody.GetProperty("id").GetGuid();

        var list = await client.GetFromJsonAsync<JsonElement>("/api/customer/addresses");
        list.GetArrayLength().Should().Be(2);

        var mark = await client.PostAsJsonAsync($"/api/customer/addresses/{secondId}/default", new { });
        mark.EnsureSuccessStatusCode();
        (await mark.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("isDefault").GetBoolean().Should().BeTrue();

        var def = await client.GetFromJsonAsync<JsonElement>("/api/customer/addresses/default");
        def.GetProperty("id").GetGuid().Should().Be(secondId);

        var afterList = await client.GetFromJsonAsync<JsonElement>("/api/customer/addresses");
        afterList.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == firstId)
            .GetProperty("isDefault").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Customer_CannotAccessOtherCustomerAddress()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var owner = await _factory.CreateAuthenticatedCustomerClientAsync();
        await AddCsrfAsync(owner);
        var created = await owner.PostAsJsonAsync("/api/customer/addresses", AddressPayload("Casa", "01310100"));
        created.EnsureSuccessStatusCode();
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var other = await _factory.CreateAuthenticatedCustomerClientAsync();
        await AddCsrfAsync(other);
        var get = await other.GetAsync("/api/customer/addresses/default");
        get.StatusCode.Should().BeOneOf(HttpStatusCode.NoContent, HttpStatusCode.OK);
        if (get.StatusCode == HttpStatusCode.OK)
        {
            var body = await get.Content.ReadFromJsonAsync<JsonElement>();
            if (body.ValueKind == JsonValueKind.Object && body.TryGetProperty("id", out var otherId))
                otherId.GetGuid().Should().NotBe(id);
        }

        var update = await other.PutAsJsonAsync($"/api/customer/addresses/{id}", AddressPayload("Hack", "01310100"));
        update.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_ValidatesCepAndUf()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = await _factory.CreateAuthenticatedCustomerClientAsync();
        await AddCsrfAsync(client);
        var created = await client.PostAsJsonAsync("/api/customer/addresses", AddressPayload("Casa", "01310100"));
        created.EnsureSuccessStatusCode();
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var invalid = await client.PutAsJsonAsync($"/api/customer/addresses/{id}", new
        {
            recipientName = "Ana",
            postalCode = "12",
            street = "Rua A",
            number = "1",
            neighborhood = "Centro",
            city = "São Paulo",
            state = "SPP"
        });
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Delete_RemovesOwnAddress()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = await _factory.CreateAuthenticatedCustomerClientAsync();
        await AddCsrfAsync(client);
        var created = await client.PostAsJsonAsync("/api/customer/addresses", AddressPayload("Casa", "01310100"));
        created.EnsureSuccessStatusCode();
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var deleted = await client.DeleteAsync($"/api/customer/addresses/{id}");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var list = await client.GetFromJsonAsync<JsonElement>("/api/customer/addresses");
        list.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Checkout_UsesSavedAddressAndRejectsForeignId()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = await _factory.CreateAuthenticatedCustomerClientAsync();
        await AddCsrfAsync(client);
        var created = await client.PostAsJsonAsync("/api/customer/addresses", AddressPayload("Casa", "01310100"));
        created.EnsureSuccessStatusCode();
        var addressId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var foreign = await client.PostAsJsonAsync("/api/checkout/sessions", new
        {
            customer = new { fullName = "Ana", email = "ana@test.local", phone = "11999999999" },
            customerAddressId = Guid.NewGuid(),
            items = Array.Empty<object>()
        });
        foreign.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity);

        var ownedMissingItems = await client.PostAsJsonAsync("/api/checkout/sessions", new
        {
            customer = new { fullName = "Ana", email = "ana@test.local", phone = "11999999999" },
            customerAddressId = addressId,
            items = Array.Empty<object>()
        });
        ownedMissingItems.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
        var problem = await ownedMissingItems.Content.ReadFromJsonAsync<JsonElement>();
        var raw = problem.ToString();
        raw.Should().NotContain("ObjectKey");
        raw.Should().NotContain("StoragePath");
    }

    private static object AddressPayload(string label, string postalCode) => new
    {
        label,
        recipientName = "Ana Silva",
        postalCode,
        street = "Av. Paulista",
        number = "1000",
        complement = (string?)null,
        neighborhood = "Bela Vista",
        city = "São Paulo",
        state = "SP"
    };

    private static async Task AddCsrfAsync(HttpClient client)
    {
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        var token = csrf.GetProperty("token").GetString();
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);
    }
}
