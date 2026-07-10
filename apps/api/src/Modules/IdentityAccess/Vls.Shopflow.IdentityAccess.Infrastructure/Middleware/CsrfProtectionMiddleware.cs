using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Vls.Shopflow.IdentityAccess.Domain.Constants;

namespace Vls.Shopflow.IdentityAccess.Infrastructure.Middleware;

/// <summary>
/// Validates CSRF tokens on authenticated cookie-based mutations.
/// SPA flow: GET /api/auth/csrf → send X-CSRF-TOKEN header on POST/PUT/PATCH/DELETE.
/// </summary>
public sealed class CsrfProtectionMiddleware(RequestDelegate next)
{
    private static readonly HashSet<string> ExcludedPathPrefixes =
    [
        "/api/auth/admin/login",
        "/api/auth/customer/login",
        "/api/auth/customer/register",
        "/api/auth/customer/forgot-password",
        "/api/auth/customer/reset-password",
        "/api/auth/customer/confirm-email",
        "/api/webhooks/",
        "/api/payments/pix/webhooks/"
    ];

    public async Task InvokeAsync(HttpContext context, IAntiforgery antiforgery)
    {
        if (await RequiresCsrfValidationAsync(context))
        {
            await CookiePrincipalResolver.EnsurePrincipalAsync(context);

            try
            {
                await antiforgery.ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new { message = "Invalid or missing CSRF token." });
                return;
            }
        }

        await next(context);
    }

    private static async Task<bool> RequiresCsrfValidationAsync(HttpContext context)
    {
        if (HttpMethods.IsGet(context.Request.Method)
            || HttpMethods.IsHead(context.Request.Method)
            || HttpMethods.IsOptions(context.Request.Method)
            || HttpMethods.IsTrace(context.Request.Method))
        {
            return false;
        }

        var path = context.Request.Path.Value ?? string.Empty;
        if (ExcludedPathPrefixes.Any(prefix =>
                path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (await HasAuthenticatedCookieAsync(context, AuthSchemes.AdminCookie))
            return true;

        return await HasAuthenticatedCookieAsync(context, AuthSchemes.CustomerCookie);
    }

    private static async Task<bool> HasAuthenticatedCookieAsync(HttpContext context, string scheme)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && string.Equals(context.User.Identity.AuthenticationType, scheme, StringComparison.Ordinal))
        {
            return true;
        }

        var result = await context.AuthenticateAsync(scheme);
        return result.Succeeded;
    }
}
