namespace Vls.Shopflow.Orders.Domain.Enums;

/// <summary>
/// Lifecycle of a grouped delivery remessa — separate from <see cref="FulfillmentStatus"/> on individual orders.
/// </summary>
public enum DeliveryBatchStatus
{
    AwaitingShipment = 0,
    Shipped = 1,
    Delivered = 2
}
