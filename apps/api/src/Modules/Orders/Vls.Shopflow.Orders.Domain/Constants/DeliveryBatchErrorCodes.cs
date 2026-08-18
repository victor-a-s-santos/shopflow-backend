namespace Vls.Shopflow.Orders.Domain.Constants;

public static class DeliveryBatchErrorCodes
{
    public const string OrderIdsRequired = "DELIVERY_BATCH_ORDER_IDS_REQUIRED";
    public const string MinOrdersRequired = "DELIVERY_BATCH_MIN_ORDERS_REQUIRED";
    public const string OrderNotFound = "DELIVERY_BATCH_ORDER_NOT_FOUND";
    public const string OrderNotPaid = "DELIVERY_BATCH_ORDER_NOT_PAID";
    public const string OrderNotEligible = "DELIVERY_BATCH_ORDER_NOT_ELIGIBLE";
    public const string OrderAlreadyShipped = "DELIVERY_BATCH_ORDER_ALREADY_SHIPPED";
    public const string OrderAlreadyDelivered = "DELIVERY_BATCH_ORDER_ALREADY_DELIVERED";
    public const string OrderAlreadyInBatch = "DELIVERY_BATCH_ORDER_ALREADY_IN_BATCH";
    public const string CustomerMismatch = "DELIVERY_BATCH_CUSTOMER_MISMATCH";
    public const string CustomerIdentityRequired = "DELIVERY_BATCH_CUSTOMER_IDENTITY_REQUIRED";
    public const string AddressMismatch = "DELIVERY_BATCH_ADDRESS_MISMATCH";
    public const string CannotBeShipped = "DELIVERY_BATCH_CANNOT_BE_SHIPPED";
    public const string CannotBeDelivered = "DELIVERY_BATCH_CANNOT_BE_DELIVERED";
    public const string MustBeShippedBeforeDelivered = "DELIVERY_BATCH_MUST_BE_SHIPPED_BEFORE_DELIVERED";
    public const string AlreadyDelivered = "DELIVERY_BATCH_ALREADY_DELIVERED";
    public const string NotFound = "DELIVERY_BATCH_NOT_FOUND";
}
