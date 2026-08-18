namespace Vls.Shopflow.IdentityAccess.Application.Interfaces;

public interface ICustomerAccessNotifier
{
    Task NotifyRegisteredPendingAsync(
        CustomerRegisteredPendingApproval notification,
        CancellationToken cancellationToken = default);

    Task NotifyApprovedAsync(
        CustomerAccessChangedNotification notification,
        CancellationToken cancellationToken = default);

    Task NotifyRejectedAsync(
        CustomerAccessChangedNotification notification,
        CancellationToken cancellationToken = default);

    Task NotifySuspendedAsync(
        CustomerAccessChangedNotification notification,
        CancellationToken cancellationToken = default);
}

/// <summary>Backward-compatible alias used by register until callers switch to <see cref="ICustomerAccessNotifier"/>.</summary>
public interface ICustomerPendingApprovalNotifier : ICustomerAccessNotifier;

public sealed record CustomerRegisteredPendingApproval(
    Guid CustomerUserId,
    string Email,
    string FullName,
    DateTimeOffset RequestedAt,
    string? Phone = null);

public sealed record CustomerAccessChangedNotification(
    Guid CustomerUserId,
    string Email,
    string FullName,
    DateTimeOffset? DecidedAt = null);
