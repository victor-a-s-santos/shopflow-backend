using Vls.Shopflow.BuildingBlocks.Application.Interfaces;
using Vls.Shopflow.Inventory.Application.DataTransferObjects;

namespace Vls.Shopflow.Inventory.Application.Queries;

public sealed record GetSkuAvailabilityBatchQuery(IReadOnlyList<Guid> SkuIds)
    : IQuery<SkuAvailabilityBatchResponseDto>;
