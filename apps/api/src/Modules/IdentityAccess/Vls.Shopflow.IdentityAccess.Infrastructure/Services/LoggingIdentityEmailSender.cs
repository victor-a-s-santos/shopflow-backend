using Microsoft.Extensions.Logging;
using Vls.Shopflow.IdentityAccess.Application.Interfaces;

namespace Vls.Shopflow.IdentityAccess.Infrastructure.Services;

/// <summary>
/// Development stub when Notifications is not registered. Never logs confirm/reset tokens.
/// </summary>
public sealed class LoggingIdentityEmailSender(ILogger<LoggingIdentityEmailSender> logger)
    : IIdentityEmailSender
{
    public Task SendEmailConfirmationAsync(string email, string token, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Email confirmation requested for {Email}. Token generated (not logged).",
            email);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string email, string token, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Password reset requested for {Email}. Token generated (not logged).",
            email);
        return Task.CompletedTask;
    }
}
