using Microsoft.Extensions.Logging;
using Vls.Shopflow.IdentityAccess.Application.Interfaces;

namespace Vls.Shopflow.IdentityAccess.Infrastructure.Services;

/// <summary>
/// Fallback when Notifications is not registered. Never logs secrets.
/// </summary>
public sealed class LoggingCustomerAccessNotifier(ILogger<LoggingCustomerAccessNotifier> logger)
    : ICustomerAccessNotifier, ICustomerPendingApprovalNotifier
{
    public Task NotifyRegisteredPendingAsync(
        CustomerRegisteredPendingApproval notification,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "CustomerRegisteredPendingApproval CustomerUserId={CustomerUserId} (e-mail enqueue skipped: logging stub).",
            notification.CustomerUserId);
        return Task.CompletedTask;
    }

    public Task NotifyApprovedAsync(
        CustomerAccessChangedNotification notification,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "CustomerApproved CustomerUserId={CustomerUserId} (e-mail enqueue skipped: logging stub).",
            notification.CustomerUserId);
        return Task.CompletedTask;
    }

    public Task NotifyRejectedAsync(
        CustomerAccessChangedNotification notification,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "CustomerRejected CustomerUserId={CustomerUserId} (e-mail enqueue skipped: logging stub).",
            notification.CustomerUserId);
        return Task.CompletedTask;
    }

    public Task NotifySuspendedAsync(
        CustomerAccessChangedNotification notification,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "CustomerSuspended CustomerUserId={CustomerUserId} (e-mail enqueue skipped: logging stub).",
            notification.CustomerUserId);
        return Task.CompletedTask;
    }
}
