namespace Vls.Shopflow.PaymentsPix.Domain.Exceptions;

public sealed class PixPaymentNotFoundException : Exception
{
    public Guid PaymentId { get; }

    public PixPaymentNotFoundException(Guid paymentId)
        : base($"Pix payment {paymentId} was not found.")
        => PaymentId = paymentId;
}

public sealed class PixPaymentNotFoundForOrderException : Exception
{
    public Guid OrderId { get; }

    public PixPaymentNotFoundForOrderException(Guid orderId)
        : base($"No Pix payment was found for order {orderId}.")
        => OrderId = orderId;
}

public sealed class OrderNotFoundForPixPaymentException : Exception
{
    public Guid OrderId { get; }

    public OrderNotFoundForPixPaymentException(Guid orderId)
        : base($"Order {orderId} was not found.")
        => OrderId = orderId;
}

public sealed class OrderNotEligibleForPixPaymentException : Exception
{
    public Guid OrderId { get; }
    public string OrderStatus { get; }

    public OrderNotEligibleForPixPaymentException(Guid orderId, string orderStatus)
        : base($"Order {orderId} cannot receive a Pix payment because its status is {orderStatus}.")
    {
        OrderId = orderId;
        OrderStatus = orderStatus;
    }
}

public sealed class InvalidOrderTotalForPixPaymentException : Exception
{
    public Guid OrderId { get; }
    public decimal Total { get; }

    public InvalidOrderTotalForPixPaymentException(Guid orderId, decimal total)
        : base($"Order {orderId} has an invalid total ({total}) for Pix payment.")
    {
        OrderId = orderId;
        Total = total;
    }
}
