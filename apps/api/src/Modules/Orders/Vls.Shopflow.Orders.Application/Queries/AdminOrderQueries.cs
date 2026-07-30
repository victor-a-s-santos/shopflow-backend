using Vls.Shopflow.BuildingBlocks.Application.Interfaces;
using Vls.Shopflow.Orders.Application.DataTransferObjects;

namespace Vls.Shopflow.Orders.Application.Queries;

public sealed record GetAdminOrdersQuery(
    int Page = 1,
    int PageSize = 20,
    string? Status = null,
    string? PaymentStatus = null,
    string? Q = null,
    DateTimeOffset? CreatedFrom = null,
    DateTimeOffset? CreatedTo = null,
    bool? PaidOnly = null,
    string? Sort = null,
    string? FulfillmentStatus = null) : IQuery<PagedAdminOrdersDto>;

public sealed record GetAdminOrderByIdQuery(Guid OrderId) : IQuery<AdminOrderDetailDto>;
