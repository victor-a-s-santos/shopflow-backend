using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vls.Shopflow.IdentityAccess.Domain.Constants;
using Vls.Shopflow.IdentityAccess.Domain.Enums;
using Vls.Shopflow.IdentityAccess.Infrastructure.Identity;
using Vls.Shopflow.IdentityAccess.Infrastructure.Options;

namespace Vls.Shopflow.IdentityAccess.Infrastructure.Seed;

public static class IdentityAccessDbContextSeed
{
    public static async Task SeedAsync(
        UserManager<ShopflowUser> userManager,
        RoleManager<ShopflowRole> roleManager,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger logger)
    {
        await EnsureRoleAsync(roleManager, AuthRoles.Owner);
        await EnsureRoleAsync(roleManager, AuthRoles.Customer);

        await SeedPrimaryAdminAsync(userManager, configuration, environment, logger);
        await SeedDemoUsersAsync(userManager, configuration, environment, logger);
    }

    private static async Task SeedPrimaryAdminAsync(
        UserManager<ShopflowUser> userManager,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger logger)
    {
        var email = configuration["SHOPFLOW_ADMIN_EMAIL"]
                    ?? configuration[$"{AdminSeedOptions.SectionName}:Email"]
                    ?? Environment.GetEnvironmentVariable("SHOPFLOW_ADMIN_EMAIL");

        var password = configuration["SHOPFLOW_ADMIN_PASSWORD"]
                       ?? configuration[$"{AdminSeedOptions.SectionName}:Password"]
                       ?? Environment.GetEnvironmentVariable("SHOPFLOW_ADMIN_PASSWORD");

        var name = configuration["SHOPFLOW_ADMIN_NAME"]
                   ?? configuration[$"{AdminSeedOptions.SectionName}:Name"]
                   ?? Environment.GetEnvironmentVariable("SHOPFLOW_ADMIN_NAME")
                   ?? "Shopflow Admin";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    "Admin seed configuration is required in non-development environments. " +
                    "Set SHOPFLOW_ADMIN_EMAIL and SHOPFLOW_ADMIN_PASSWORD.");
            }

            logger.LogWarning(
                "Admin seed skipped: SHOPFLOW_ADMIN_EMAIL or SHOPFLOW_ADMIN_PASSWORD not configured.");
            return;
        }

        var normalizedEmail = email.Trim();
        var resetPassword = IsFlagEnabled(configuration, "SHOPFLOW_ADMIN_RESET_PASSWORD");
        var existing = await userManager.FindByEmailAsync(normalizedEmail);
        if (existing is not null)
        {
            if (resetPassword)
            {
                await ResetPasswordAsync(userManager, existing, password, normalizedEmail, "Admin", logger);
                return;
            }

            logger.LogInformation("Admin seed skipped: user {Email} already exists.", normalizedEmail);
            return;
        }

        await CreateStaffOwnerAsync(userManager, normalizedEmail, password, name.Trim(), logger);
        logger.LogInformation("Admin user seeded for {Email}.", normalizedEmail);
    }

    private static async Task SeedDemoUsersAsync(
        UserManager<ShopflowUser> userManager,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger logger)
    {
        if (environment.IsProduction())
        {
            logger.LogInformation("Demo users seed skipped: Production.");
            return;
        }

        var options = ReadDemoUsersOptions(configuration);
        if (!IsDemoUsersEnabled(configuration, environment, options))
        {
            logger.LogInformation("Demo users seed skipped: SHOPFLOW_DEMO_USERS_ENABLED is not true.");
            return;
        }

        await EnsureDemoAdminAsync(userManager, options, logger);
        await EnsureDemoCustomerAsync(userManager, options, logger);
    }

    private static async Task EnsureDemoAdminAsync(
        UserManager<ShopflowUser> userManager,
        DemoUsersSeedOptions options,
        ILogger logger)
    {
        var email = options.AdminEmail.Trim();
        var password = options.AdminPassword;
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Demo admin seed skipped: email or password not configured.");
            return;
        }

        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            if (!existing.IsStaff)
            {
                logger.LogWarning(
                    "Demo admin seed skipped: {Email} exists and is not a staff user.",
                    email);
                return;
            }

            await EnsureStaffOwnerAsync(userManager, existing, logger);
            if (options.ResetPasswords)
                await ResetPasswordAsync(userManager, existing, password, email, "Demo admin", logger);
            else
                logger.LogInformation("Demo admin seed skipped: user {Email} already exists.", email);

            return;
        }

        await CreateStaffOwnerAsync(userManager, email, password, options.AdminName.Trim(), logger);
        logger.LogInformation("Demo admin user seeded for {Email}.", email);
    }

    private static async Task EnsureDemoCustomerAsync(
        UserManager<ShopflowUser> userManager,
        DemoUsersSeedOptions options,
        ILogger logger)
    {
        var email = options.CustomerEmail.Trim();
        var password = options.CustomerPassword;
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Demo customer seed skipped: email or password not configured.");
            return;
        }

        if (string.Equals(email, options.AdminEmail.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("Demo customer seed skipped: customer email matches demo admin email.");
            return;
        }

        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            if (existing.IsStaff)
            {
                logger.LogWarning(
                    "Demo customer seed skipped: {Email} exists and is a staff user.",
                    email);
                return;
            }

            await EnsureApprovedCustomerAsync(userManager, existing, logger);
            if (options.ResetPasswords)
                await ResetPasswordAsync(userManager, existing, password, email, "Demo customer", logger);
            else
                logger.LogInformation("Demo customer seed skipped create: user {Email} already exists.", email);

            return;
        }

        var user = new ShopflowUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = options.CustomerName.Trim(),
            IsStaff = false,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            AccessStatus = CustomerAccessStatus.Approved,
            ApprovedAt = DateTimeOffset.UtcNow,
            AccessDecidedAt = DateTimeOffset.UtcNow
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to seed demo customer: {errors}");
        }

        await userManager.AddToRoleAsync(user, AuthRoles.Customer);
        logger.LogInformation("Demo customer user seeded for {Email}.", email);
    }

    private static async Task CreateStaffOwnerAsync(
        UserManager<ShopflowUser> userManager,
        string email,
        string password,
        string name,
        ILogger logger)
    {
        var user = new ShopflowUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = name,
            IsStaff = true,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            AccessStatus = CustomerAccessStatus.Approved,
            ApprovedAt = DateTimeOffset.UtcNow
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to seed admin user: {errors}");
        }

        await userManager.AddToRoleAsync(user, AuthRoles.Owner);
        logger.LogDebug("Staff Owner created for {Email}.", email);
    }

    private static async Task EnsureStaffOwnerAsync(
        UserManager<ShopflowUser> userManager,
        ShopflowUser user,
        ILogger logger)
    {
        var changed = false;
        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            changed = true;
        }

        if (!user.IsActive)
        {
            user.IsActive = true;
            changed = true;
        }

        if (changed)
        {
            var update = await userManager.UpdateAsync(user);
            if (!update.Succeeded)
            {
                var errors = string.Join("; ", update.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to update demo admin: {errors}");
            }
        }

        if (!await userManager.IsInRoleAsync(user, AuthRoles.Owner))
            await userManager.AddToRoleAsync(user, AuthRoles.Owner);

        logger.LogDebug("Demo admin {Email} already present.", user.Email);
    }

    private static async Task EnsureApprovedCustomerAsync(
        UserManager<ShopflowUser> userManager,
        ShopflowUser user,
        ILogger logger)
    {
        var changed = false;
        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            changed = true;
        }

        if (!user.IsActive)
        {
            user.IsActive = true;
            changed = true;
        }

        if (user.AccessStatus != CustomerAccessStatus.Approved)
        {
            user.AccessStatus = CustomerAccessStatus.Approved;
            user.ApprovedAt ??= DateTimeOffset.UtcNow;
            user.AccessDecidedAt = DateTimeOffset.UtcNow;
            changed = true;
        }

        if (changed)
        {
            var update = await userManager.UpdateAsync(user);
            if (!update.Succeeded)
            {
                var errors = string.Join("; ", update.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to approve demo customer: {errors}");
            }

            logger.LogInformation("Demo customer {Email} marked Approved.", user.Email);
        }

        if (!await userManager.IsInRoleAsync(user, AuthRoles.Customer))
            await userManager.AddToRoleAsync(user, AuthRoles.Customer);
    }

    private static bool IsDemoUsersEnabled(
        IConfiguration configuration,
        IHostEnvironment environment,
        DemoUsersSeedOptions options)
    {
        var raw = configuration["SHOPFLOW_DEMO_USERS_ENABLED"]
                  ?? Environment.GetEnvironmentVariable("SHOPFLOW_DEMO_USERS_ENABLED");
        if (bool.TryParse(raw, out var enabled))
            return enabled;

        if (options.Enabled)
            return true;

        // TESTE (Testing) liga o seed sem flag explícita; HML/PROD continuam off.
        return string.Equals(environment.EnvironmentName, "Testing", StringComparison.OrdinalIgnoreCase);
    }

    private static DemoUsersSeedOptions ReadDemoUsersOptions(IConfiguration configuration)
    {
        var options = configuration.GetSection(DemoUsersSeedOptions.SectionName).Get<DemoUsersSeedOptions>()
                      ?? new DemoUsersSeedOptions();

        options.Enabled = ReadBool(configuration, "SHOPFLOW_DEMO_USERS_ENABLED", options.Enabled);
        options.ResetPasswords = ReadBool(configuration, "SHOPFLOW_DEMO_USERS_RESET_PASSWORD", options.ResetPasswords);
        options.AdminEmail = ReadString(configuration, "SHOPFLOW_DEMO_ADMIN_EMAIL", options.AdminEmail);
        options.AdminPassword = ReadString(configuration, "SHOPFLOW_DEMO_ADMIN_PASSWORD", options.AdminPassword);
        options.AdminName = ReadString(configuration, "SHOPFLOW_DEMO_ADMIN_NAME", options.AdminName);
        options.CustomerEmail = ReadString(configuration, "SHOPFLOW_DEMO_CUSTOMER_EMAIL", options.CustomerEmail);
        options.CustomerPassword = ReadString(configuration, "SHOPFLOW_DEMO_CUSTOMER_PASSWORD", options.CustomerPassword);
        options.CustomerName = ReadString(configuration, "SHOPFLOW_DEMO_CUSTOMER_NAME", options.CustomerName);
        return options;
    }

    private static bool ReadBool(IConfiguration configuration, string key, bool fallback)
    {
        var raw = configuration[key] ?? Environment.GetEnvironmentVariable(key);
        return bool.TryParse(raw, out var value) ? value : fallback;
    }

    private static string ReadString(IConfiguration configuration, string key, string fallback)
    {
        var raw = configuration[key] ?? Environment.GetEnvironmentVariable(key);
        return string.IsNullOrWhiteSpace(raw) ? fallback : raw.Trim();
    }

    private static bool IsFlagEnabled(IConfiguration configuration, string key)
    {
        var raw = configuration[key] ?? Environment.GetEnvironmentVariable(key);
        return bool.TryParse(raw, out var enabled) && enabled;
    }

    private static async Task ResetPasswordAsync(
        UserManager<ShopflowUser> userManager,
        ShopflowUser user,
        string newPassword,
        string normalizedEmail,
        string label,
        ILogger logger)
    {
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to reset {label} password: {errors}");
        }

        // Password rotation should clear lockouts from prior failed attempts (esp. local/TESTE).
        await userManager.ResetAccessFailedCountAsync(user);
        await userManager.SetLockoutEndDateAsync(user, null);

        logger.LogInformation("{Label} password reset for {Email}.", label, normalizedEmail);
    }

    private static async Task EnsureRoleAsync(RoleManager<ShopflowRole> roleManager, string roleName)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            var result = await roleManager.CreateAsync(new ShopflowRole { Name = roleName });
            if (!result.Succeeded)
            {
                var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to create role {roleName}: {errors}");
            }
        }
    }
}
