using MediatR;
using Vls.Shopflow.IdentityAccess.Application.DataTransferObjects;
using Vls.Shopflow.IdentityAccess.Domain.Enums;

namespace Vls.Shopflow.IdentityAccess.Application.Queries;

public sealed record GetStoreAccessQuery : IRequest<StoreAccessDto>;

public sealed record GetAdminCustomersQuery(
    CustomerAccessStatus? Status,
    string? Search,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedAdminCustomersDto>;

public sealed record GetPendingCustomerCountQuery : IRequest<PendingCustomerCountDto>;

public sealed record GetAdminCustomerByIdQuery(Guid CustomerId) : IRequest<AdminCustomerListItemDto?>;
