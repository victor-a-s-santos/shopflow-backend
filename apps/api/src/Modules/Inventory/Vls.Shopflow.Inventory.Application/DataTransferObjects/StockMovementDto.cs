namespace Vls.Shopflow.Inventory.Application.DataTransferObjects;

public sealed record StockMovementDto(
    Guid Id,
    Guid SkuId,
    string Type,
    int Quantity,
    string? Reason,
    DateTimeOffset CreatedAt);
