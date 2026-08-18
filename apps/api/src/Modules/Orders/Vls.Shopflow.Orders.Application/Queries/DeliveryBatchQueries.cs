using Vls.Shopflow.BuildingBlocks.Application.Interfaces;
using Vls.Shopflow.Orders.Application.DataTransferObjects;

namespace Vls.Shopflow.Orders.Application.Queries;

public sealed record GetDeliveryBatchCandidatesQuery(Guid OrderId)
    : IQuery<DeliveryBatchCandidatesDto>;

public sealed record GetDeliveryBatchesQuery(
    int Page = 1,
    int PageSize = 20,
    string? Status = null,
    string? Q = null,
    string? CustomerEmail = null,
    DateTimeOffset? CreatedFrom = null,
    DateTimeOffset? CreatedTo = null,
    string? Sort = null) : IQuery<PagedDeliveryBatchesDto>;

public sealed record GetDeliveryBatchByIdQuery(Guid BatchId) : IQuery<DeliveryBatchDetailDto>;
