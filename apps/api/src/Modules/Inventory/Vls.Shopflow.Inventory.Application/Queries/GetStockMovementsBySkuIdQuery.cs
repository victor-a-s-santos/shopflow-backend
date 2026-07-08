using Vls.Shopflow.BuildingBlocks.Application.Interfaces;
using Vls.Shopflow.Inventory.Application.DataTransferObjects;

namespace Vls.Shopflow.Inventory.Application.Queries;

public sealed record GetStockMovementsBySkuIdQuery(
    Guid SkuId,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedStockMovementsDto>;
