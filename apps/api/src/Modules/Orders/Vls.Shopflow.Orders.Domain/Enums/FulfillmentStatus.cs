namespace Vls.Shopflow.Orders.Domain.Enums;

/// <summary>
/// Logistics / shipment lifecycle — separate from <see cref="OrderStatus"/> and Pix payment status.
/// </summary>
public enum FulfillmentStatus
{
    AwaitingShipment = 0,
    Shipped = 1,
    Delivered = 2
}
