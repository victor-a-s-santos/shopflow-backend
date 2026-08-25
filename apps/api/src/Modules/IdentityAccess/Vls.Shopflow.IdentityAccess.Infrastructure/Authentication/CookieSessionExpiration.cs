using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging;

namespace Vls.Shopflow.IdentityAccess.Infrastructure.Authentication;

/// <summary>
/// Caps cookie sliding expiration with an absolute lifetime stamped at login.
/// Sliding refresh resets IssuedUtc; this custom item is preserved across renewals.
/// </summary>
public static class CookieSessionExpiration
{
    public const string SessionStartedItemKey = "sf.session_started";
    public const string AbsoluteSecondsItemKey = "sf.absolute_seconds";

    public static void Stamp(
        AuthenticationProperties properties,
        DateTimeOffset startedUtc,
        TimeSpan absoluteLifetime)
    {
        ArgumentNullException.ThrowIfNull(properties);

        var seconds = Math.Max(1, (long)Math.Ceiling(absoluteLifetime.TotalSeconds));
        properties.Items[SessionStartedItemKey] = startedUtc.ToUnixTimeSeconds().ToString();
        properties.Items[AbsoluteSecondsItemKey] = seconds.ToString();
    }

    public static bool TryGetSessionStarted(AuthenticationProperties properties, out DateTimeOffset startedUtc)
    {
        ArgumentNullException.ThrowIfNull(properties);
        startedUtc = default;

        if (!properties.Items.TryGetValue(SessionStartedItemKey, out var raw)
            || string.IsNullOrWhiteSpace(raw)
            || !long.TryParse(raw, out var unixSeconds))
        {
            return false;
        }

        startedUtc = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        return true;
    }

    public static bool TryGetAbsoluteLifetime(AuthenticationProperties properties, out TimeSpan absoluteLifetime)
    {
        ArgumentNullException.ThrowIfNull(properties);
        absoluteLifetime = default;

        if (!properties.Items.TryGetValue(AbsoluteSecondsItemKey, out var raw)
            || string.IsNullOrWhiteSpace(raw)
            || !long.TryParse(raw, out var seconds)
            || seconds <= 0)
        {
            return false;
        }

        absoluteLifetime = TimeSpan.FromSeconds(seconds);
        return true;
    }

    public static bool IsAbsoluteExpired(
        DateTimeOffset nowUtc,
        DateTimeOffset startedUtc,
        TimeSpan absoluteLifetime)
        => nowUtc - startedUtc >= absoluteLifetime;

    public static async Task ValidateAbsoluteAsync(
        CookieValidatePrincipalContext context,
        string scheme,
        TimeSpan configuredAbsoluteLifetime,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        if (context.Principal?.Identity?.IsAuthenticated != true)
            return;

        var now = (context.Options.TimeProvider ?? TimeProvider.System).GetUtcNow();

        if (!TryGetSessionStarted(context.Properties, out var startedUtc))
        {
            startedUtc = context.Properties.IssuedUtc ?? now;
            var absolute = TryGetAbsoluteLifetime(context.Properties, out var stored)
                ? stored
                : configuredAbsoluteLifetime;
            Stamp(context.Properties, startedUtc, absolute);
            context.ShouldRenew = true;
        }

        var lifetime = TryGetAbsoluteLifetime(context.Properties, out var ticketLifetime)
            ? ticketLifetime
            : configuredAbsoluteLifetime;

        if (!IsAbsoluteExpired(now, startedUtc, lifetime))
            return;

        logger.LogInformation(
            "Session rejected due to absolute expiration. Scheme={Scheme}",
            scheme);

        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(scheme);
    }

    public static void LogIdleExpirationIfPresent(
        AuthenticateResult result,
        string scheme,
        ILogger logger)
    {
        if (result.Failure?.Message?.Contains("expired", StringComparison.OrdinalIgnoreCase) != true)
            return;

        logger.LogInformation("Session rejected due to idle expiration. Scheme={Scheme}", scheme);
    }
}
