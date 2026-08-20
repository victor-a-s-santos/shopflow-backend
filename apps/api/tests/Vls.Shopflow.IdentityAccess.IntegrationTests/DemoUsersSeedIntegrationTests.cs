using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vls.Shopflow.IdentityAccess.Domain.Constants;
using Vls.Shopflow.IdentityAccess.Domain.Enums;
using Vls.Shopflow.IdentityAccess.Infrastructure.Identity;
using Vls.Shopflow.IdentityAccess.Infrastructure.Seed;

namespace Vls.Shopflow.IdentityAccess.IntegrationTests;

public sealed class DemoUsersSeedIntegrationTests : IClassFixture<ShopflowWebApplicationFactory>
{
    private readonly ShopflowWebApplicationFactory _factory;

    public DemoUsersSeedIntegrationTests(ShopflowWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SeedAsync_WhenDemoEnabled_CreatesApprovedCustomerAndOwnerAdmin()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var adminEmail = $"demo-admin-{Guid.NewGuid():N}@teste.local";
        var customerEmail = $"demo-customer-{Guid.NewGuid():N}@teste.local";

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ShopflowUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ShopflowRole>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("DemoUsersSeedIntegrationTests");

        await IdentityAccessDbContextSeed.SeedAsync(
            userManager,
            roleManager,
            BuildDemoConfig(adminEmail, customerEmail, enabled: true),
            StubHostEnvironment.Development(),
            logger);

        var admin = await userManager.FindByEmailAsync(adminEmail);
        admin.Should().NotBeNull();
        admin!.IsStaff.Should().BeTrue();
        admin.AccessStatus.Should().Be(CustomerAccessStatus.Approved);
        (await userManager.IsInRoleAsync(admin, AuthRoles.Owner)).Should().BeTrue();
        (await userManager.CheckPasswordAsync(admin, "Admin123")).Should().BeTrue();

        var customer = await userManager.FindByEmailAsync(customerEmail);
        customer.Should().NotBeNull();
        customer!.IsStaff.Should().BeFalse();
        customer.EmailConfirmed.Should().BeTrue();
        customer.AccessStatus.Should().Be(CustomerAccessStatus.Approved);
        customer.ApprovedAt.Should().NotBeNull();
        (await userManager.IsInRoleAsync(customer, AuthRoles.Customer)).Should().BeTrue();
        (await userManager.CheckPasswordAsync(customer, "Teste123")).Should().BeTrue();
    }

    [Fact]
    public async Task SeedAsync_WhenDemoDisabled_DoesNotCreateUsers()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var adminEmail = $"demo-off-admin-{Guid.NewGuid():N}@teste.local";
        var customerEmail = $"demo-off-customer-{Guid.NewGuid():N}@teste.local";

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ShopflowUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ShopflowRole>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("DemoUsersSeedIntegrationTests");

        await IdentityAccessDbContextSeed.SeedAsync(
            userManager,
            roleManager,
            BuildDemoConfig(adminEmail, customerEmail, enabled: false),
            StubHostEnvironment.Development(),
            logger);

        (await userManager.FindByEmailAsync(adminEmail)).Should().BeNull();
        (await userManager.FindByEmailAsync(customerEmail)).Should().BeNull();
    }

    [Fact]
    public async Task SeedAsync_WhenProduction_DoesNotCreateDemoUsersEvenIfEnabled()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var primaryAdminEmail = $"primary-admin-{Guid.NewGuid():N}@teste.local";
        var demoAdminEmail = $"demo-prod-admin-{Guid.NewGuid():N}@teste.local";
        var customerEmail = $"demo-prod-customer-{Guid.NewGuid():N}@teste.local";

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ShopflowUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ShopflowRole>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("DemoUsersSeedIntegrationTests");

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SHOPFLOW_ADMIN_EMAIL"] = primaryAdminEmail,
                ["SHOPFLOW_ADMIN_PASSWORD"] = "PrimaryAdmin1",
                ["SHOPFLOW_DEMO_USERS_ENABLED"] = "true",
                ["SHOPFLOW_DEMO_ADMIN_EMAIL"] = demoAdminEmail,
                ["SHOPFLOW_DEMO_ADMIN_PASSWORD"] = "Admin123",
                ["SHOPFLOW_DEMO_CUSTOMER_EMAIL"] = customerEmail,
                ["SHOPFLOW_DEMO_CUSTOMER_PASSWORD"] = "Teste123"
            })
            .Build();

        await IdentityAccessDbContextSeed.SeedAsync(
            userManager,
            roleManager,
            config,
            StubHostEnvironment.Production(),
            logger);

        (await userManager.FindByEmailAsync(demoAdminEmail)).Should().BeNull();
        (await userManager.FindByEmailAsync(customerEmail)).Should().BeNull();
        (await userManager.FindByEmailAsync(primaryAdminEmail)).Should().NotBeNull();
    }

    [Fact]
    public async Task SeedAsync_WhenCustomerExistsPending_ApprovesWithoutChangingPassword()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var adminEmail = $"demo-pending-admin-{Guid.NewGuid():N}@teste.local";
        var customerEmail = $"demo-pending-customer-{Guid.NewGuid():N}@teste.local";
        const string originalPassword = "OriginalPass1";

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ShopflowUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ShopflowRole>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("DemoUsersSeedIntegrationTests");

        var pending = new ShopflowUser
        {
            Id = Guid.NewGuid(),
            UserName = customerEmail,
            Email = customerEmail,
            EmailConfirmed = false,
            FullName = "Pending Customer",
            IsStaff = false,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            AccessStatus = CustomerAccessStatus.PendingApproval,
            AccessRequestedAt = DateTimeOffset.UtcNow
        };
        (await userManager.CreateAsync(pending, originalPassword)).Succeeded.Should().BeTrue();
        await userManager.AddToRoleAsync(pending, AuthRoles.Customer);

        await IdentityAccessDbContextSeed.SeedAsync(
            userManager,
            roleManager,
            BuildDemoConfig(adminEmail, customerEmail, enabled: true, resetPasswords: false),
            StubHostEnvironment.Development(),
            logger);

        var customer = await userManager.FindByEmailAsync(customerEmail);
        customer.Should().NotBeNull();
        customer!.AccessStatus.Should().Be(CustomerAccessStatus.Approved);
        customer.EmailConfirmed.Should().BeTrue();
        (await userManager.CheckPasswordAsync(customer, originalPassword)).Should().BeTrue();
        (await userManager.CheckPasswordAsync(customer, "Teste123")).Should().BeFalse();
    }

    [Fact]
    public async Task SeedAsync_WhenDemoUserExistsAndResetTrue_ResetsPassword()
    {
        if (!await _factory.CanConnectToDatabaseAsync())
            return;

        var adminEmail = $"demo-reset-admin-{Guid.NewGuid():N}@teste.local";
        var customerEmail = $"demo-reset-customer-{Guid.NewGuid():N}@teste.local";

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ShopflowUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ShopflowRole>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("DemoUsersSeedIntegrationTests");

        await IdentityAccessDbContextSeed.SeedAsync(
            userManager,
            roleManager,
            BuildDemoConfig(adminEmail, customerEmail, enabled: true, adminPassword: "FirstPass1", customerPassword: "FirstPass1"),
            StubHostEnvironment.Development(),
            logger);

        await IdentityAccessDbContextSeed.SeedAsync(
            userManager,
            roleManager,
            BuildDemoConfig(
                adminEmail,
                customerEmail,
                enabled: true,
                resetPasswords: true,
                adminPassword: "Admin123",
                customerPassword: "Teste123"),
            StubHostEnvironment.Development(),
            logger);

        var admin = await userManager.FindByEmailAsync(adminEmail);
        var customer = await userManager.FindByEmailAsync(customerEmail);
        (await userManager.CheckPasswordAsync(admin!, "FirstPass1")).Should().BeFalse();
        (await userManager.CheckPasswordAsync(admin!, "Admin123")).Should().BeTrue();
        (await userManager.CheckPasswordAsync(customer!, "FirstPass1")).Should().BeFalse();
        (await userManager.CheckPasswordAsync(customer!, "Teste123")).Should().BeTrue();
    }

    private static IConfiguration BuildDemoConfig(
        string adminEmail,
        string customerEmail,
        bool enabled,
        bool resetPasswords = false,
        string adminPassword = "Admin123",
        string customerPassword = "Teste123")
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SHOPFLOW_DEMO_USERS_ENABLED"] = enabled ? "true" : "false",
                ["SHOPFLOW_DEMO_USERS_RESET_PASSWORD"] = resetPasswords ? "true" : "false",
                ["SHOPFLOW_DEMO_ADMIN_EMAIL"] = adminEmail,
                ["SHOPFLOW_DEMO_ADMIN_PASSWORD"] = adminPassword,
                ["SHOPFLOW_DEMO_ADMIN_NAME"] = "Admin Teste",
                ["SHOPFLOW_DEMO_CUSTOMER_EMAIL"] = customerEmail,
                ["SHOPFLOW_DEMO_CUSTOMER_PASSWORD"] = customerPassword,
                ["SHOPFLOW_DEMO_CUSTOMER_NAME"] = "Cliente Teste"
            })
            .Build();
    }

    private sealed class StubHostEnvironment : IHostEnvironment
    {
        public required string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "DemoUsersSeedTests";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

        public static StubHostEnvironment Development() => new() { EnvironmentName = Environments.Development };

        public static StubHostEnvironment Production() => new() { EnvironmentName = Environments.Production };
    }
}
