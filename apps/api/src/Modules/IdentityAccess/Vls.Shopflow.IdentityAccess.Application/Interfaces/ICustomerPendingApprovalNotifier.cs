using Vls.Shopflow.IdentityAccess.Application.DataTransferObjects;

namespace Vls.Shopflow.IdentityAccess.Application.Interfaces;

public interface ICustomerPendingApprovalNotifier
{
    Task NotifyRegisteredPendingAsync(
        CustomerRegisteredPendingApproval notification,
        CancellationToken cancellationToken = default);
}

public sealed record CustomerRegisteredPendingApproval(
    Guid CustomerUserId,
    string Email,
    string FullName,
    DateTimeOffset RequestedAt);
