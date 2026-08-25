using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using Vls.Shopflow.IdentityAccess.Domain.Constants;

namespace Vls.Shopflow.IdentityAccess.Infrastructure.Authentication;

/// <summary>
/// Applies the DI <see cref="TimeProvider"/> to admin and customer cookie schemes so tests can fake time.
/// </summary>
internal sealed class CookieTimeProviderPostConfigure(TimeProvider timeProvider)
    : IPostConfigureOptions<CookieAuthenticationOptions>
{
    public void PostConfigure(string? name, CookieAuthenticationOptions options)
    {
        if (name is AuthSchemes.AdminCookie or AuthSchemes.CustomerCookie)
            options.TimeProvider = timeProvider;
    }
}
