using Vls.Shopflow.IdentityAccess.Domain.Enums;

namespace Vls.Shopflow.IdentityAccess.Application.DataTransferObjects;

public sealed record AdminCustomerListItemDto(
    Guid CustomerId,
    string Email,
    string FullName,
    string? Phone,
    bool EmailConfirmed,
    CustomerAccessStatus AccessStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset? AccessRequestedAt,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset? AccessDecidedAt,
    Guid? AccessDecidedByAdminUserId,
    string? AccessDecisionReason);

public sealed record PagedAdminCustomersDto(
    IReadOnlyList<AdminCustomerListItemDto> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);

public sealed record PendingCustomerCountDto(int PendingCount);
