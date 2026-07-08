using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vls.Shopflow.IdentityAccess.Domain.Constants;
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
        var resetPassword = IsResetPasswordEnabled(configuration);
        var existing = await userManager.FindByEmailAsync(normalizedEmail);
        if (existing is not null)
        {
            if (resetPassword)
            {
                await ResetAdminPasswordAsync(userManager, existing, password, normalizedEmail, logger);
                return;
            }

            logger.LogInformation("Admin seed skipped: user {Email} already exists.", normalizedEmail);
            return;
        }

        var user = new ShopflowUser
        {
            Id = Guid.NewGuid(),
            UserName = normalizedEmail,
            Email = normalizedEmail,
            EmailConfirmed = true,
            FullName = name.Trim(),
            IsStaff = true,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to seed admin user: {errors}");
        }

        await userManager.AddToRoleAsync(user, AuthRoles.Owner);
        logger.LogInformation("Admin user seeded for {Email}.", normalizedEmail);
    }

    private static bool IsResetPasswordEnabled(IConfiguration configuration)
    {
        var raw = configuration["SHOPFLOW_ADMIN_RESET_PASSWORD"]
                  ?? Environment.GetEnvironmentVariable("SHOPFLOW_ADMIN_RESET_PASSWORD");

        return bool.TryParse(raw, out var enabled) && enabled;
    }

    private static async Task ResetAdminPasswordAsync(
        UserManager<ShopflowUser> userManager,
        ShopflowUser user,
        string newPassword,
        string normalizedEmail,
        ILogger logger)
    {
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to reset admin password: {errors}");
        }

        logger.LogInformation("Admin password reset for {Email}.", normalizedEmail);
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
