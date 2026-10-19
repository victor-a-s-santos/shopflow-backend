using Vls.Shopflow.BuildingBlocks.Application.Interfaces;
using Vls.Shopflow.Orders.Application.DataTransferObjects;

namespace Vls.Shopflow.Orders.Application.Queries;

public sealed record GetAdminProductDemandQuery(
    int Page = 1,
    int PageSize = 20,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    string? Q = null,
    string? Sort = null) : IQuery<PagedAdminProductDemandDto>;

public sealed record GetAdminProductDemandOrdersQuery(
    Guid SkuId,
    int Page = 1,
    int PageSize = 20,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    string? Sort = null) : IQuery<PagedAdminProductDemandOrdersDto>;

public sealed record GetAdminUnpaidOrdersQuery(
    int Page = 1,
    int PageSize = 20,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    string? Q = null,
    string? Sort = null) : IQuery<PagedAdminUnpaidOrdersDto>;
