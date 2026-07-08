using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Vls.Shopflow.IdentityAccess.Domain.Constants;

namespace Vls.Shopflow.IdentityAccess.Infrastructure.Middleware;

/// <summary>
/// Resolves <see cref="HttpContext.User"/> from admin or customer cookie schemes.
/// Default authentication only uses the admin scheme; customer sessions need explicit resolution.
/// </summary>
public sealed class CookiePrincipalMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        await CookiePrincipalResolver.EnsurePrincipalAsync(context);
        await next(context);
    }
}

internal static class CookiePrincipalResolver
{
    public static async Task EnsurePrincipalAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
            return;

        var path = context.Request.Path.Value ?? string.Empty;

        if (path.StartsWith("/api/auth/admin/", StringComparison.OrdinalIgnoreCase))
        {
            await TrySetPrincipalAsync(context, AuthSchemes.AdminCookie);
            return;
        }

        if (path.StartsWith("/api/auth/customer/", StringComparison.OrdinalIgnoreCase))
        {
            await TrySetPrincipalAsync(context, AuthSchemes.CustomerCookie);
            return;
        }

        if (await TrySetPrincipalAsync(context, AuthSchemes.CustomerCookie))
            return;

        await TrySetPrincipalAsync(context, AuthSchemes.AdminCookie);
    }

    private static async Task<bool> TrySetPrincipalAsync(HttpContext context, string scheme)
    {
        var result = await context.AuthenticateAsync(scheme);
        if (!result.Succeeded || result.Principal is null)
            return false;

        context.User = result.Principal;
        return true;
    }
}
