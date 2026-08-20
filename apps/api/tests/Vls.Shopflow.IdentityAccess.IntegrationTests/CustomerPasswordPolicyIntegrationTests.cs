using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Vls.Shopflow.IdentityAccess.Application.Security;

namespace Vls.Shopflow.IdentityAccess.IntegrationTests;

public sealed class CustomerPasswordPolicyIntegrationTests : IClassFixture<ShopflowWebApplicationFactory>
{
    private readonly ShopflowWebApplicationFactory _factory;

    public CustomerPasswordPolicyIntegrationTests(ShopflowWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("ab1A!", "Use pelo menos 8 caracteres.")]
    [InlineData("nouppercase1!", "Use pelo menos uma letra maiúscula.")]
    [InlineData("NOLOWERCASE1!", "Use pelo menos uma letra minúscula.")]
    [InlineData("NoDigitHere!", "Use pelo menos um número.")]
    [InlineData("NoSpecial123", "Use pelo menos um caractere especial.")]
    public async Task Register_WeakPassword_Returns400WithPasswordErrors(string password, string expectedMessage)
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/customer/register", new
        {
            email = $"weak-{Guid.NewGuid():N}@test.local",
            password,
            fullName = "Weak Password User",
            phone = "11999999999"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        body.TryGetProperty("errors", out var errors).Should().BeTrue();
        errors.TryGetProperty("password", out var passwordErrors).Should().BeTrue();
        passwordErrors.EnumerateArray().Select(e => e.GetString()).Should().Contain(expectedMessage);
        body.ToString().Should().NotContain("at Vls.Shopflow");
        body.ToString().Should().NotContain("StackTrace");
    }

    [Fact]
    public async Task Register_StrongPassword_CreatesApprovedCustomerInOpenMode()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = _factory.CreateClient();
        var email = $"strong-{Guid.NewGuid():N}@test.local";

        var response = await client.PostAsJsonAsync("/api/auth/customer/register", new
        {
            email,
            password = CustomerPasswordPolicy.DevTestExamplePassword,
            fullName = "Strong Password User",
            phone = "11999999999"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("email").GetString().Should().Be(email);
        body.GetProperty("approvalStatus").GetString().Should().Be("Approved");
    }

    [Fact]
    public async Task ResetPassword_WeakPassword_Returns400WithPasswordErrors()
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
            newPassword = "weakpass"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("errors", out var errors).Should().BeTrue();
        var field = errors.TryGetProperty("newPassword", out var np) ? np
            : errors.TryGetProperty("password", out var p) ? p
            : default;
        field.ValueKind.Should().Be(JsonValueKind.Array);
        field.GetArrayLength().Should().BeGreaterThan(0);
        body.ToString().Should().NotContain("StackTrace");
    }

    [Fact]
    public async Task ResetPassword_StrongPassword_Succeeds()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var email = await _factory.RegisterCustomerAsync();
        var client = _factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/customer/forgot-password", new { email });
        var token = _factory.EmailSender.GetResetToken(email);
        token.Should().NotBeNullOrEmpty();

        var newPassword = "NewShopflow@456";
        var response = await client.PostAsJsonAsync("/api/auth/customer/reset-password", new
        {
            email,
            token,
            newPassword
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var login = await client.PostAsJsonAsync("/api/auth/customer/login", new
        {
            email,
            password = newPassword
        });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task IdentityOptions_RequireUppercaseAndSpecial()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        using var scope = _factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<IdentityOptions>>();
        options.Value.Password.RequireUppercase.Should().BeTrue();
        options.Value.Password.RequireNonAlphanumeric.Should().BeTrue();
        options.Value.Password.RequireDigit.Should().BeTrue();
        options.Value.Password.RequireLowercase.Should().BeTrue();
        options.Value.Password.RequiredLength.Should().Be(8);
    }
}

public sealed class CustomerPasswordPolicyClosedModeTests : IClassFixture<PrivateStoreAccessWebApplicationFactory>
{
    private readonly PrivateStoreAccessWebApplicationFactory _factory;

    public CustomerPasswordPolicyClosedModeTests(PrivateStoreAccessWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_StrongPassword_InClosedMode_CreatesPending()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var client = _factory.CreateClient();
        var email = $"pending-pwd-{Guid.NewGuid():N}@test.local";

        var response = await client.PostAsJsonAsync("/api/auth/customer/register", new
        {
            email,
            password = CustomerPasswordPolicy.DevTestExamplePassword,
            fullName = "Pending Strong",
            phone = "11999999999"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("approvalStatus").GetString().Should().Be("Pending");
    }
}
