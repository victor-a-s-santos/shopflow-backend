using Vls.Shopflow.IdentityAccess.Application.DataTransferObjects;
using Vls.Shopflow.IdentityAccess.Domain.Enums;

namespace Vls.Shopflow.IdentityAccess.Application.Interfaces;

public interface ICustomerApprovalAdminService
{
    Task<PagedAdminCustomersDto> ListAsync(
        CustomerAccessStatus? status,
        string? search,
        int page,
        int pageSize,
        DateTimeOffset? createdFrom = null,
        DateTimeOffset? createdTo = null,
        string? sort = null,
        CancellationToken cancellationToken = default);

    Task<int> CountPendingAsync(CancellationToken cancellationToken = default);

    Task<AdminCustomerListItemDto?> GetByIdAsync(Guid customerId, CancellationToken cancellationToken = default);

    Task<AdminCustomerListItemDto> ApproveAsync(
        Guid customerId,
        Guid adminUserId,
        string? reason,
        CancellationToken cancellationToken = default);

    Task<AdminCustomerListItemDto> RejectAsync(
        Guid customerId,
        Guid adminUserId,
        string? reason,
        CancellationToken cancellationToken = default);

    Task<AdminCustomerListItemDto> SuspendAsync(
        Guid customerId,
        Guid adminUserId,
        string? reason,
        CancellationToken cancellationToken = default);

    Task<AdminCustomerListItemDto> ReactivateAsync(
        Guid customerId,
        Guid adminUserId,
        string? reason,
        CancellationToken cancellationToken = default);
}
