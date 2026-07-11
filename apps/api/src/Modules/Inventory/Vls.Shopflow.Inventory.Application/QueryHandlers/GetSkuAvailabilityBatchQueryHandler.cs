using Vls.Shopflow.BuildingBlocks.Application.Interfaces;
using Vls.Shopflow.Inventory.Application.DataTransferObjects;
using Vls.Shopflow.Inventory.Application.Queries;
using Vls.Shopflow.Inventory.Application.Repositories;

namespace Vls.Shopflow.Inventory.Application.QueryHandlers;

public sealed class GetSkuAvailabilityBatchQueryHandler(
    IInventoryReadModel readModel)
    : IQueryHandler<GetSkuAvailabilityBatchQuery, SkuAvailabilityBatchResponseDto>
{
    public async Task<SkuAvailabilityBatchResponseDto> Handle(
        GetSkuAvailabilityBatchQuery query,
        CancellationToken cancellationToken)
    {
        var found = await readModel.GetBySkuIdsAsync(query.SkuIds, cancellationToken);
        var bySkuId = found.ToDictionary(x => x.SkuId);

        var items = new List<SkuAvailabilityBatchItemDto>(query.SkuIds.Count);
        foreach (var skuId in query.SkuIds)
        {
            if (bySkuId.TryGetValue(skuId, out var item))
            {
                items.Add(new SkuAvailabilityBatchItemDto(
                    item.SkuId,
                    item.AvailableQuantity,
                    item.QuantityOnHand,
                    item.QuantityReserved,
                    Exists: true));
            }
            else
            {
                items.Add(new SkuAvailabilityBatchItemDto(
                    skuId,
                    AvailableQuantity: null,
                    QuantityOnHand: null,
                    ReservedQuantity: null,
                    Exists: false));
            }
        }

        return new SkuAvailabilityBatchResponseDto(items);
    }
}
