using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Vls.Shopflow.IdentityAccess.Application.Interfaces;
using Vls.Shopflow.IdentityAccess.Domain.Constants;
using Vls.Shopflow.IdentityAccess.Infrastructure.Identity;

namespace Vls.Shopflow.IdentityAccess.Infrastructure.Services;

public sealed class AdminSignInService(
    SignInManager<ShopflowUser> signInManager,
    IHttpContextAccessor httpContextAccessor)
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

        await httpContext.SignOutAsync(AuthSchemes.AdminCookie);
        await signInManager.SignInAsync(user, isPersistent: false);
        return (true, null);
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
            return;

        await httpContext.SignOutAsync(AuthSchemes.AdminCookie);
    }
}

public sealed class CurrentAdminAccessor(
    IHttpContextAccessor httpContextAccessor,
    UserManager<ShopflowUser> userManager)
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
            return null;

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
