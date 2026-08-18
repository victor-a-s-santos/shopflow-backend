namespace Vls.Shopflow.Inventory.Application.DataTransferObjects;

public sealed record SkuAvailabilityBatchItemDto(
    Guid SkuId,
    int? AvailableQuantity,
    int? QuantityOnHand,
    int? ReservedQuantity,
    bool Exists);

public sealed record SkuAvailabilityBatchResponseDto(
    IReadOnlyList<SkuAvailabilityBatchItemDto> Items);
