namespace Vls.Shopflow.Inventory.Domain.Enums;

public enum StockMovementType
{
    InitialStockAdded = 1,
    StockAdded = 2,
    StockRemoved = 3,
    StockReserved = 4,
    ReservationConfirmed = 5,
    ReservationCanceled = 6
}
