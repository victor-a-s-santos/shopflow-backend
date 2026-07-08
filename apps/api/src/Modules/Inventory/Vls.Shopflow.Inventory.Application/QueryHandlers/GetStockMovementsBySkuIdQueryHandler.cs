using Vls.Shopflow.BuildingBlocks.Application.Interfaces;
using Vls.Shopflow.Inventory.Application.DataTransferObjects;
using Vls.Shopflow.Inventory.Application.Queries;
using Vls.Shopflow.Inventory.Application.Repositories;
using Vls.Shopflow.Inventory.Domain.Exceptions;

namespace Vls.Shopflow.Inventory.Application.QueryHandlers;

public sealed class GetStockMovementsBySkuIdQueryHandler(
    IInventoryReadModel inventoryReadModel,
    IStockMovementReadModel readModel)
    : IQueryHandler<GetStockMovementsBySkuIdQuery, PagedStockMovementsDto>
{
    public async Task<PagedStockMovementsDto> Handle(
        GetStockMovementsBySkuIdQuery query,
        CancellationToken cancellationToken)
    {
        var inventory = await inventoryReadModel.GetBySkuIdAsync(query.SkuId, cancellationToken);
        if (inventory is null)
            throw new InventoryItemNotFoundException(query.SkuId);

        return await readModel.GetBySkuIdAsync(
            query.SkuId, query.Page, query.PageSize, cancellationToken);
    }
}
