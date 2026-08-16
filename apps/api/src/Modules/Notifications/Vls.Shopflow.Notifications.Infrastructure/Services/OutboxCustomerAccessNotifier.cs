using Microsoft.Extensions.Logging;
using Vls.Shopflow.IdentityAccess.Application.Interfaces;
using Vls.Shopflow.Notifications.Application.Interfaces;

namespace Vls.Shopflow.Notifications.Infrastructure.Services;

/// <summary>
/// Enqueues customer-approval e-mails to the outbox. Never throws to callers.
/// </summary>
public sealed class OutboxCustomerAccessNotifier(
    IEmailNotificationService notifications,
    ILogger<OutboxCustomerAccessNotifier> logger)
    : ICustomerAccessNotifier, ICustomerPendingApprovalNotifier
{
    public Task NotifyRegisteredPendingAsync(
        CustomerRegisteredPendingApproval notification,
        CancellationToken cancellationToken = default)
        => SafeAsync(
            "CustomerRegisteredPendingApproval",
            notification.CustomerUserId,
            async () =>
            {
                var request = ToRequest(notification);
                await notifications.EnqueueCustomerApprovalRequestAdminAsync(request, cancellationToken);
                await notifications.EnqueueCustomerRegistrationReceivedAsync(request, cancellationToken);
            });

    public Task NotifyApprovedAsync(
        CustomerAccessChangedNotification notification,
        CancellationToken cancellationToken = default)
        => SafeAsync(
            "CustomerApproved",
            notification.CustomerUserId,
            () => notifications.EnqueueCustomerApprovedAsync(ToRequest(notification), cancellationToken));

    public Task NotifyRejectedAsync(
        CustomerAccessChangedNotification notification,
        CancellationToken cancellationToken = default)
        => SafeAsync(
            "CustomerRejected",
            notification.CustomerUserId,
            () => notifications.EnqueueCustomerRejectedAsync(ToRequest(notification), cancellationToken));

    public Task NotifySuspendedAsync(
        CustomerAccessChangedNotification notification,
        CancellationToken cancellationToken = default)
        => SafeAsync(
            "CustomerSuspended",
            notification.CustomerUserId,
            () => notifications.EnqueueCustomerSuspendedAsync(ToRequest(notification), cancellationToken));

    private async Task SafeAsync(string eventName, Guid customerUserId, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to enqueue {Event} e-mail for CustomerUserId={CustomerUserId}",
                eventName,
                customerUserId);
        }
    }

    private static CustomerApprovalEmailRequest ToRequest(CustomerRegisteredPendingApproval n)
        => new(n.CustomerUserId, n.Email, n.FullName, n.Phone, n.RequestedAt);

    private static CustomerApprovalEmailRequest ToRequest(CustomerAccessChangedNotification n)
        => new(n.CustomerUserId, n.Email, n.FullName, DecidedAt: n.DecidedAt);
}
