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
    public const string OrderStockAlreadyConfirmed = "ORDER_STOCK_ALREADY_CONFIRMED";
    public const string OrderStockConfirmationRequired = "ORDER_STOCK_CONFIRMATION_REQUIRED";
    public const string OrderMustBePaidBeforeStockConfirmation = "ORDER_MUST_BE_PAID_BEFORE_STOCK_CONFIRMATION";
    public const string OrderCannotConfirmStockAfterDelivered = "ORDER_CANNOT_CONFIRM_STOCK_AFTER_DELIVERED";
    public const string StockConfirmationNoteTooLong = "STOCK_CONFIRMATION_NOTE_TOO_LONG";
}
