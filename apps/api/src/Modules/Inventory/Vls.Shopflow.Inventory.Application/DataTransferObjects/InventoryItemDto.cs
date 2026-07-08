namespace Vls.Shopflow.Inventory.Application.DataTransferObjects;

public sealed record InventoryItemDto(
    Guid SkuId,
    int QuantityOnHand,
    int QuantityReserved,
    int AvailableQuantity);
