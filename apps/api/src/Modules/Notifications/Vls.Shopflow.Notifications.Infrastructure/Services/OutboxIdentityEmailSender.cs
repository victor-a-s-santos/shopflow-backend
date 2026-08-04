using Microsoft.Extensions.Logging;
using Vls.Shopflow.IdentityAccess.Application.Interfaces;
using Vls.Shopflow.Notifications.Application.Interfaces;

namespace Vls.Shopflow.Notifications.Infrastructure.Services;

/// <summary>
/// Replaces logging stub: enqueues confirm/reset emails to the outbox (never blocks on Brevo).
/// </summary>
public sealed class OutboxIdentityEmailSender(
    IEmailNotificationService notifications,
    ILogger<OutboxIdentityEmailSender> logger) : IIdentityEmailSender
{
    public async Task SendEmailConfirmationAsync(
        string email,
        string token,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await notifications.EnqueueConfirmEmailAsync(email, email, token, cancellationToken);
        }
        catch (Exception ex)
        {
            // Auth must not fail if outbox write fails — log and continue (same spirit as prior stub).
            logger.LogError(ex, "Failed to enqueue confirmation email for {Email}", email);
        }
    }

    public async Task SendPasswordResetAsync(
        string email,
        string token,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await notifications.EnqueueResetPasswordAsync(email, null, token, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to enqueue password reset email for {Email}", email);
        }
    }
}
