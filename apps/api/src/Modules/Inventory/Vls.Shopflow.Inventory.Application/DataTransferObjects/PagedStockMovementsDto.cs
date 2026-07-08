namespace Vls.Shopflow.Inventory.Application.DataTransferObjects;

public sealed record PagedStockMovementsDto(
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    IReadOnlyList<StockMovementDto> Items);
