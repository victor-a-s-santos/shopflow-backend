using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vls.Shopflow.IdentityAccess.Infrastructure.Identity;
using Vls.Shopflow.IdentityAccess.Infrastructure.Seed;

namespace Vls.Shopflow.IdentityAccess.IntegrationTests;

public sealed class AdminSeedIntegrationTests : IClassFixture<ShopflowWebApplicationFactory>
{
    private readonly ShopflowWebApplicationFactory _factory;

    public AdminSeedIntegrationTests(ShopflowWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SeedAsync_WhenAdminExistsAndResetTrue_ResetsPassword()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var email = $"admin-reset-{Guid.NewGuid():N}@test.local";
        const string originalPassword = "OriginalPass1";
        const string newPassword = "NewResetPass2";

        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var userManager = services.GetRequiredService<UserManager<ShopflowUser>>();
        var roleManager = services.GetRequiredService<RoleManager<ShopflowRole>>();
        var environment = services.GetRequiredService<IHostEnvironment>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("AdminSeedIntegrationTests");

        var createConfig = BuildSeedConfig(email, originalPassword, resetPassword: false);
        await IdentityAccessDbContextSeed.SeedAsync(
            userManager,
            roleManager,
            createConfig,
            environment,
            logger);

        var resetConfig = BuildSeedConfig(email, newPassword, resetPassword: true);
        await IdentityAccessDbContextSeed.SeedAsync(
            userManager,
            roleManager,
            resetConfig,
            environment,
            logger);

        var user = await userManager.FindByEmailAsync(email);
        user.Should().NotBeNull();

        (await userManager.CheckPasswordAsync(user!, originalPassword)).Should().BeFalse();
        (await userManager.CheckPasswordAsync(user!, newPassword)).Should().BeTrue();
    }

    [Fact]
    public async Task SeedAsync_WhenAdminExistsAndResetFalse_KeepsExistingPassword()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var email = $"admin-skip-{Guid.NewGuid():N}@test.local";
        const string originalPassword = "OriginalPass1";
        const string otherPassword = "OtherPass123";

        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var userManager = services.GetRequiredService<UserManager<ShopflowUser>>();
        var roleManager = services.GetRequiredService<RoleManager<ShopflowRole>>();
        var environment = services.GetRequiredService<IHostEnvironment>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("AdminSeedIntegrationTests");

        var createConfig = BuildSeedConfig(email, originalPassword, resetPassword: false);
        await IdentityAccessDbContextSeed.SeedAsync(
            userManager,
            roleManager,
            createConfig,
            environment,
            logger);

        var skipConfig = BuildSeedConfig(email, otherPassword, resetPassword: false);
        await IdentityAccessDbContextSeed.SeedAsync(
            userManager,
            roleManager,
            skipConfig,
            environment,
            logger);

        var user = await userManager.FindByEmailAsync(email);
        user.Should().NotBeNull();

        (await userManager.CheckPasswordAsync(user!, originalPassword)).Should().BeTrue();
        (await userManager.CheckPasswordAsync(user!, otherPassword)).Should().BeFalse();
    }

    private static IConfiguration BuildSeedConfig(string email, string password, bool resetPassword)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SHOPFLOW_ADMIN_EMAIL"] = email,
                ["SHOPFLOW_ADMIN_PASSWORD"] = password,
                ["SHOPFLOW_ADMIN_NAME"] = "Seed Test Admin",
                ["SHOPFLOW_ADMIN_RESET_PASSWORD"] = resetPassword ? "true" : "false"
            })
            .Build();
    }
}
