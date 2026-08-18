namespace Vls.Shopflow.Orders.Domain.Constants;

public static class OrderFulfillmentErrorCodes
{
    public const string DeliveryDateTooSoon = "DELIVERY_DATE_TOO_SOON";
    public const string InvalidDeliveryMethod = "INVALID_DELIVERY_METHOD";
    public const string OrderNotPaidForShipment = "ORDER_NOT_PAID_FOR_SHIPMENT";
    public const string OrderCannotBeShipped = "ORDER_CANNOT_BE_SHIPPED";
    public const string OrderCannotBeDelivered = "ORDER_CANNOT_BE_DELIVERED";
    public const string OrderMustBeShippedBeforeDelivered = "ORDER_MUST_BE_SHIPPED_BEFORE_DELIVERED";
    public const string InternalNoteTooLong = "INTERNAL_NOTE_TOO_LONG";
    public const string CustomerOrderNoteTooLong = "CUSTOMER_ORDER_NOTE_TOO_LONG";
    public const string TrackingCodeTooLong = "TRACKING_CODE_TOO_LONG";
}
