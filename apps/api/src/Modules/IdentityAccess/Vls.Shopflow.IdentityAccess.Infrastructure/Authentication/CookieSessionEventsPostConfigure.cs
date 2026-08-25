using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vls.Shopflow.IdentityAccess.Domain.Constants;
using Vls.Shopflow.IdentityAccess.Infrastructure.Options;

namespace Vls.Shopflow.IdentityAccess.Infrastructure.Authentication;

/// <summary>
/// Chains absolute session timeout after any existing OnValidatePrincipal (Identity security stamp).
/// </summary>
internal sealed class CookieSessionEventsPostConfigure : IPostConfigureOptions<CookieAuthenticationOptions>
{
    public void PostConfigure(string? name, CookieAuthenticationOptions options)
    {
        if (name is not (AuthSchemes.AdminCookie or AuthSchemes.CustomerCookie))
            return;

        var previous = options.Events.OnValidatePrincipal;
        var scheme = name;

        options.Events.OnValidatePrincipal = async context =>
        {
            if (previous is not null)
                await previous(context);

            if (context.Principal?.Identity?.IsAuthenticated != true)
                return;

            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("Shopflow.Auth.Session");

            if (scheme == AuthSchemes.AdminCookie)
            {
                var opts = context.HttpContext.RequestServices
                    .GetRequiredService<IOptions<AdminAuthOptions>>()
                    .Value;
                await CookieSessionExpiration.ValidateAbsoluteAsync(
                    context,
                    scheme,
                    opts.GetAbsoluteLifetime(),
                    logger);
                return;
            }

            var customerOpts = context.HttpContext.RequestServices
                .GetRequiredService<IOptions<CustomerAuthOptions>>()
                .Value;
            await CookieSessionExpiration.ValidateAbsoluteAsync(
                context,
                scheme,
                customerOpts.GetAbsoluteLifetime(),
                logger);
        };
    }
}
