namespace Vls.Shopflow.Inventory.Domain.Exceptions;

public class InventoryItemNotFoundException : Exception
{
    public Guid SkuId { get; }

    public InventoryItemNotFoundException(Guid skuId)
        : base($"Inventory not found for SKU {skuId}.")
        => SkuId = skuId;
}

public class InventoryItemAlreadyExistsException : Exception
{
    public Guid SkuId { get; }

    public InventoryItemAlreadyExistsException(Guid skuId)
        : base($"Inventory already exists for SKU {skuId}.")
        => SkuId = skuId;
}

public class InsufficientStockException : Exception
{
    public Guid SkuId { get; }
    public int Requested { get; }
    public int Available { get; }

    public InsufficientStockException(Guid skuId, int requested, int available)
        : base($"Insufficient stock for SKU {skuId}. Requested {requested}, available {available}.")
    {
        SkuId = skuId;
        Requested = requested;
        Available = available;
    }
}

public class StockReservationNotFoundException : Exception
{
    public Guid ReservationId { get; }

    public StockReservationNotFoundException(Guid reservationId)
        : base($"Stock reservation {reservationId} not found.")
        => ReservationId = reservationId;
}

public class InvalidStockReservationStatusException : Exception
{
    public Guid ReservationId { get; }

    public InvalidStockReservationStatusException(Guid reservationId, string message)
        : base(message)
        => ReservationId = reservationId;
}

public class InvalidStockQuantityException : Exception
{
    public InvalidStockQuantityException(string message) : base(message) { }
}

public class SkuNotFoundException : Exception
{
    public Guid SkuId { get; }

    public SkuNotFoundException(Guid skuId)
        : base($"SKU {skuId} was not found in catalog.")
        => SkuId = skuId;
}
