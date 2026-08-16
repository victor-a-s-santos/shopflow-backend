using MediatR;
using Vls.Shopflow.IdentityAccess.Application.DataTransferObjects;

namespace Vls.Shopflow.IdentityAccess.Application.Commands;

public sealed record ApproveCustomerCommand(
    Guid CustomerId,
    Guid AdminUserId,
    string? Reason) : IRequest<AdminCustomerListItemDto>;

public sealed record RejectCustomerCommand(
    Guid CustomerId,
    Guid AdminUserId,
    string? Reason) : IRequest<AdminCustomerListItemDto>;

public sealed record SuspendCustomerCommand(
    Guid CustomerId,
    Guid AdminUserId,
    string? Reason) : IRequest<AdminCustomerListItemDto>;

public sealed record ReactivateCustomerCommand(
    Guid CustomerId,
    Guid AdminUserId,
    string? Reason) : IRequest<AdminCustomerListItemDto>;
