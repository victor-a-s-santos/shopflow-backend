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

/// <summary>
/// Opaque denial for guest order access — same message whether order/token is missing or invalid.
/// </summary>
public sealed class GuestOrderAccessDeniedException : Exception
{
    public GuestOrderAccessDeniedException()
        : base("Order access denied.")
    {
    }
}

public sealed class GuestOrderAccessMisconfiguredException : Exception
{
    public GuestOrderAccessMisconfiguredException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Guest tried to create an account but the order email already has a customer account.
/// </summary>
public sealed class GuestOrderAccountAlreadyExistsException : Exception
{
    public const string ErrorCode = "AccountAlreadyExists";

    public GuestOrderAccountAlreadyExistsException()
        : base("Já existe uma conta com este e-mail. Faça login para vincular o pedido.")
    {
    }
}

/// <summary>
/// Claim denied for a logged-in customer (e.g. email mismatch) without leaking order details.
/// </summary>
public sealed class GuestOrderClaimForbiddenException : Exception
{
    public GuestOrderClaimForbiddenException()
        : base("Unable to claim this order.")
    {
    }
}

public sealed class OrderAlreadyLinkedToAnotherCustomerException : Exception
{
    public Guid OrderId { get; }

    public OrderAlreadyLinkedToAnotherCustomerException(Guid orderId)
        : base("This order cannot be linked.")
        => OrderId = orderId;
}
