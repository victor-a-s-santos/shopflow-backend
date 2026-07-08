namespace Vls.Shopflow.Inventory.Application.DataTransferObjects;

/// <summary>
/// Safe subset for storefront — no internal stock breakdown.
/// </summary>
public sealed record SkuAvailabilityDto(
    Guid SkuId,
    bool IsAvailable,
    int AvailableQuantity);
