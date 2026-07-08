using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Vls.Shopflow.IdentityAccess.Application.Commands;
using Vls.Shopflow.IdentityAccess.Application.DataTransferObjects;
using Vls.Shopflow.IdentityAccess.Application.Interfaces;
using Vls.Shopflow.IdentityAccess.Infrastructure.Identity;

namespace Vls.Shopflow.IdentityAccess.Infrastructure.Services;

public sealed class AdminLoginService(
    UserManager<ShopflowUser> userManager,
    IAdminSignInService adminSignInService,
    ILogger<AdminLoginService> logger)
    : IAdminLoginService
{
    private const string GenericError = "Invalid email or password.";

    public async Task<AdminLoginResult> LoginAsync(
        string email,
        string password,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var trimmedEmail = email.Trim();
        var user = await userManager.FindByEmailAsync(trimmedEmail);

        if (user is null || !user.IsStaff || !user.IsActive)
        {
            logger.LogWarning(
                "Admin login failed for {Email} from {IpAddress}: user not found or not eligible.",
                trimmedEmail,
                ipAddress ?? "unknown");
            return new AdminLoginResult(false, null, GenericError);
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            logger.LogWarning(
                "Admin login locked out for {Email} from {IpAddress}.",
                trimmedEmail,
                ipAddress ?? "unknown");
            return new AdminLoginResult(false, null, "Account temporarily locked. Try again later.");
        }

        if (!await userManager.CheckPasswordAsync(user, password))
        {
            await userManager.AccessFailedAsync(user);
            logger.LogWarning(
                "Admin login failed for {Email} from {IpAddress}: invalid credentials.",
                trimmedEmail,
                ipAddress ?? "unknown");
            return new AdminLoginResult(false, null, GenericError);
        }

        await userManager.ResetAccessFailedCountAsync(user);

        var (signInSucceeded, _) = await adminSignInService.SignInAsync(user.Id, cancellationToken);
        if (!signInSucceeded)
        {
            logger.LogWarning(
                "Admin login sign-in failed for {Email} from {IpAddress}.",
                trimmedEmail,
                ipAddress ?? "unknown");
            return new AdminLoginResult(false, null, GenericError);
        }

        user.LastLoginAt = DateTimeOffset.UtcNow;
        await userManager.UpdateAsync(user);

        var roles = await userManager.GetRolesAsync(user);
        var dto = new AdminUserDto(
            user.Id,
            user.FullName ?? user.Email ?? string.Empty,
            user.Email ?? string.Empty,
            roles.ToList());

        logger.LogInformation(
            "Admin login succeeded for {UserId} ({Email}) from {IpAddress}.",
            user.Id,
            user.Email,
            ipAddress ?? "unknown");

        return new AdminLoginResult(true, dto, null);
    }
}
