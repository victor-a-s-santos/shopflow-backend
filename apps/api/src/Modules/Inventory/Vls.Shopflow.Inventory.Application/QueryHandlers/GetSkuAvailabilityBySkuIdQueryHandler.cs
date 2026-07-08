using Vls.Shopflow.BuildingBlocks.Application.Interfaces;
using Vls.Shopflow.Inventory.Application.DataTransferObjects;
using Vls.Shopflow.Inventory.Application.Queries;
using Vls.Shopflow.Inventory.Application.Repositories;

namespace Vls.Shopflow.Inventory.Application.QueryHandlers;

public sealed class GetSkuAvailabilityBySkuIdQueryHandler(
    IInventoryReadModel readModel)
    : IQueryHandler<GetSkuAvailabilityBySkuIdQuery, SkuAvailabilityDto?>
{
    public async Task<SkuAvailabilityDto?> Handle(
        GetSkuAvailabilityBySkuIdQuery query,
        CancellationToken cancellationToken)
    {
        var item = await readModel.GetBySkuIdAsync(query.SkuId, cancellationToken);
        if (item is null)
            return null;

        return new SkuAvailabilityDto(
            item.SkuId,
            item.AvailableQuantity > 0,
            Math.Max(0, item.AvailableQuantity));
    }
}
