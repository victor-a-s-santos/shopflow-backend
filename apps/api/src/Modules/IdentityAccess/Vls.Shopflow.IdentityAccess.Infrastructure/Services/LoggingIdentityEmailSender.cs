using Microsoft.Extensions.Logging;
using Vls.Shopflow.IdentityAccess.Application.Interfaces;

namespace Vls.Shopflow.IdentityAccess.Infrastructure.Services;

/// <summary>
/// Logs email tokens in Development only — real email delivery is a future phase.
/// </summary>
public sealed class LoggingIdentityEmailSender(ILogger<LoggingIdentityEmailSender> logger)
    : IIdentityEmailSender
{
    public Task SendEmailConfirmationAsync(string email, string token, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Email confirmation requested for {Email}. Token generated (not logged in production).",
            email);
        logger.LogDebug("Email confirmation token for {Email}: {Token}", email, token);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string email, string token, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Password reset requested for {Email}. Token generated (not logged in production).",
            email);
        logger.LogDebug("Password reset token for {Email}: {Token}", email, token);
        return Task.CompletedTask;
    }
}
