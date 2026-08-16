using MediatR;
using Vls.Shopflow.IdentityAccess.Application.Commands;
using Vls.Shopflow.IdentityAccess.Application.DataTransferObjects;
using Vls.Shopflow.IdentityAccess.Application.Interfaces;
using Vls.Shopflow.IdentityAccess.Application.Queries;

namespace Vls.Shopflow.IdentityAccess.Application.QueryHandlers;

public sealed class GetStoreAccessQueryHandler(IStoreAccessPolicy policy)
    : IRequestHandler<GetStoreAccessQuery, StoreAccessDto>
{
    public Task<StoreAccessDto> Handle(GetStoreAccessQuery request, CancellationToken cancellationToken)
        => Task.FromResult(policy.ToPublicDto());
}

public sealed class GetAdminCustomersQueryHandler(ICustomerApprovalAdminService service)
    : IRequestHandler<GetAdminCustomersQuery, PagedAdminCustomersDto>
{
    public Task<PagedAdminCustomersDto> Handle(GetAdminCustomersQuery request, CancellationToken cancellationToken)
        => service.ListAsync(request.Status, request.Search, request.Page, request.PageSize, cancellationToken);
}

public sealed class GetPendingCustomerCountQueryHandler(ICustomerApprovalAdminService service)
    : IRequestHandler<GetPendingCustomerCountQuery, PendingCustomerCountDto>
{
    public async Task<PendingCustomerCountDto> Handle(
        GetPendingCustomerCountQuery request,
        CancellationToken cancellationToken)
    {
        var count = await service.CountPendingAsync(cancellationToken);
        return new PendingCustomerCountDto(count);
    }
}

public sealed class GetAdminCustomerByIdQueryHandler(ICustomerApprovalAdminService service)
    : IRequestHandler<GetAdminCustomerByIdQuery, AdminCustomerListItemDto?>
{
    public Task<AdminCustomerListItemDto?> Handle(
        GetAdminCustomerByIdQuery request,
        CancellationToken cancellationToken)
        => service.GetByIdAsync(request.CustomerId, cancellationToken);
}
