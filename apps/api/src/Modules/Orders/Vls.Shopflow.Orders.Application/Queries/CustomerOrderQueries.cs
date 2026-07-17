using Vls.Shopflow.BuildingBlocks.Application.Interfaces;
using Vls.Shopflow.Orders.Application.DataTransferObjects;

namespace Vls.Shopflow.Orders.Application.Queries;

public sealed record GetCustomerOrdersQuery(
    Guid CustomerUserId,
    int Page = 1,
    int PageSize = 10,
    string? Status = null,
    string? PaymentStatus = null,
    DateTimeOffset? CreatedFrom = null,
    DateTimeOffset? CreatedTo = null,
    string? Sort = null) : IQuery<PagedCustomerOrdersDto>;

public sealed record GetCustomerOrderByIdQuery(
    Guid CustomerUserId,
    Guid OrderId) : IQuery<CustomerOrderDetailDto>;
