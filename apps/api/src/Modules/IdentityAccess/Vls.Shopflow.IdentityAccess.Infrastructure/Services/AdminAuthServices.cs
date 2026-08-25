using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vls.Shopflow.IdentityAccess.Application.Interfaces;
using Vls.Shopflow.IdentityAccess.Domain.Constants;
using Vls.Shopflow.IdentityAccess.Infrastructure.Authentication;
using Vls.Shopflow.IdentityAccess.Infrastructure.Identity;
using Vls.Shopflow.IdentityAccess.Infrastructure.Options;

namespace Vls.Shopflow.IdentityAccess.Infrastructure.Services;

public sealed class AdminSignInService(
    SignInManager<ShopflowUser> signInManager,
    IHttpContextAccessor httpContextAccessor,
    TimeProvider timeProvider,
    IOptions<AdminAuthOptions> adminAuthOptions,
    ILogger<AdminSignInService> logger)
    : IAdminSignInService
{
    public async Task<(bool Succeeded, string? ErrorMessage)> SignInAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await signInManager.UserManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsStaff || !user.IsActive)
            return (false, "Invalid email or password.");

        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("HttpContext is not available.");

        var now = timeProvider.GetUtcNow();
        var properties = new AuthenticationProperties
        {
            IsPersistent = false,
            AllowRefresh = true
        };
        CookieSessionExpiration.Stamp(properties, now, adminAuthOptions.Value.GetAbsoluteLifetime());

        await httpContext.SignOutAsync(AuthSchemes.AdminCookie);
        await signInManager.SignInAsync(user, properties);
        return (true, null);
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
            return;

        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        await httpContext.SignOutAsync(AuthSchemes.AdminCookie);
        logger.LogInformation("Admin logout. UserId={UserId}", userId ?? "unknown");
    }
}

public sealed class CurrentAdminAccessor(
    IHttpContextAccessor httpContextAccessor,
    UserManager<ShopflowUser> userManager,
    ILogger<CurrentAdminAccessor> logger)
    : ICurrentAdminAccessor
{
    public async Task<Application.DataTransferObjects.AdminUserDto?> GetCurrentAdminAsync(
        CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
            return null;

        var authResult = await httpContext.AuthenticateAsync(AuthSchemes.AdminCookie);
        if (!authResult.Succeeded || authResult.Principal is null)
        {
            CookieSessionExpiration.LogIdleExpirationIfPresent(
                authResult,
                AuthSchemes.AdminCookie,
                logger);
            return null;
        }

        var userIdClaim = authResult.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
            return null;

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsStaff || !user.IsActive)
            return null;

        var roles = await userManager.GetRolesAsync(user);
        return new Application.DataTransferObjects.AdminUserDto(
            user.Id,
            user.FullName ?? user.Email ?? string.Empty,
            user.Email ?? string.Empty,
            roles.ToList());
    }
}
