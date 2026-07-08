using Vls.Shopflow.BuildingBlocks.Application.Interfaces;
using Vls.Shopflow.Inventory.Application.DataTransferObjects;
using Vls.Shopflow.Inventory.Application.Queries;
using Vls.Shopflow.Inventory.Application.Repositories;

namespace Vls.Shopflow.Inventory.Application.QueryHandlers;

public sealed class GetInventoryBySkuIdQueryHandler(
    IInventoryReadModel readModel)
    : IQueryHandler<GetInventoryBySkuIdQuery, InventoryItemDto?>
{
    public Task<InventoryItemDto?> Handle(GetInventoryBySkuIdQuery query, CancellationToken cancellationToken)
        => readModel.GetBySkuIdAsync(query.SkuId, cancellationToken);
}
