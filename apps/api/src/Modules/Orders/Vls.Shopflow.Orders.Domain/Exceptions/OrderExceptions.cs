namespace Vls.Shopflow.Orders.Domain.Exceptions;

public sealed class OrderNotFoundException : Exception
{
    public Guid OrderId { get; }

    public OrderNotFoundException(Guid orderId)
        : base($"Order {orderId} was not found.")
        => OrderId = orderId;
}

public sealed class CheckoutSessionNotFoundForOrderException : Exception
{
    public Guid CheckoutSessionId { get; }

    public CheckoutSessionNotFoundForOrderException(Guid checkoutSessionId)
        : base($"Checkout session {checkoutSessionId} was not found.")
        => CheckoutSessionId = checkoutSessionId;
}

public sealed class InvalidCheckoutSessionForOrderException : Exception
{
    public Guid CheckoutSessionId { get; }

    public InvalidCheckoutSessionForOrderException(Guid checkoutSessionId, string message)
        : base(message)
        => CheckoutSessionId = checkoutSessionId;
}

public sealed class OrderAlreadyExistsForCheckoutSessionException : Exception
{
    public Guid CheckoutSessionId { get; }
    public Guid ExistingOrderId { get; }

    public OrderAlreadyExistsForCheckoutSessionException(Guid checkoutSessionId, Guid existingOrderId)
        : base($"An order already exists for checkout session {checkoutSessionId}.")
    {
        CheckoutSessionId = checkoutSessionId;
        ExistingOrderId = existingOrderId;
    }
}

public sealed class OrderNotFoundByCheckoutSessionException : Exception
{
    public Guid CheckoutSessionId { get; }

    public OrderNotFoundByCheckoutSessionException(Guid checkoutSessionId)
        : base($"No order was found for checkout session {checkoutSessionId}.")
        => CheckoutSessionId = checkoutSessionId;
}
