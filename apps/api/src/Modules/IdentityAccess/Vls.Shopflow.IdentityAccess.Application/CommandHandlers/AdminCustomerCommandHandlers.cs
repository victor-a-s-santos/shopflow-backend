using MediatR;
using Vls.Shopflow.IdentityAccess.Application.Commands;
using Vls.Shopflow.IdentityAccess.Application.DataTransferObjects;
using Vls.Shopflow.IdentityAccess.Application.Interfaces;

namespace Vls.Shopflow.IdentityAccess.Application.CommandHandlers;

public sealed class ApproveCustomerCommandHandler(ICustomerApprovalAdminService service)
    : IRequestHandler<ApproveCustomerCommand, AdminCustomerListItemDto>
{
    public Task<AdminCustomerListItemDto> Handle(ApproveCustomerCommand request, CancellationToken cancellationToken)
        => service.ApproveAsync(request.CustomerId, request.AdminUserId, request.Reason, cancellationToken);
}

public sealed class RejectCustomerCommandHandler(ICustomerApprovalAdminService service)
    : IRequestHandler<RejectCustomerCommand, AdminCustomerListItemDto>
{
    public Task<AdminCustomerListItemDto> Handle(RejectCustomerCommand request, CancellationToken cancellationToken)
        => service.RejectAsync(request.CustomerId, request.AdminUserId, request.Reason, cancellationToken);
}

public sealed class SuspendCustomerCommandHandler(ICustomerApprovalAdminService service)
    : IRequestHandler<SuspendCustomerCommand, AdminCustomerListItemDto>
{
    public Task<AdminCustomerListItemDto> Handle(SuspendCustomerCommand request, CancellationToken cancellationToken)
        => service.SuspendAsync(request.CustomerId, request.AdminUserId, request.Reason, cancellationToken);
}

public sealed class ReactivateCustomerCommandHandler(ICustomerApprovalAdminService service)
    : IRequestHandler<ReactivateCustomerCommand, AdminCustomerListItemDto>
{
    public Task<AdminCustomerListItemDto> Handle(
        ReactivateCustomerCommand request,
        CancellationToken cancellationToken)
        => service.ReactivateAsync(request.CustomerId, request.AdminUserId, request.Reason, cancellationToken);
}
