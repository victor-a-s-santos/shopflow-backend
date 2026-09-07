using Vls.Shopflow.Orders.Domain.Constants;

namespace Vls.Shopflow.Orders.Domain.Exceptions;

public class OrderFulfillmentException : Exception
{
    public string Code { get; }

    public OrderFulfillmentException(string code, string message)
        : base(message)
        => Code = code;
}

public sealed class OrderNotPaidForShipmentException : OrderFulfillmentException
{
    public Guid OrderId { get; }

    public OrderNotPaidForShipmentException(Guid orderId)
        : base(
            OrderFulfillmentErrorCodes.OrderNotPaidForShipment,
            "Este pedido ainda não pode ser marcado como enviado.")
        => OrderId = orderId;
}

public sealed class OrderCannotBeShippedException : OrderFulfillmentException
{
    public Guid OrderId { get; }

    public OrderCannotBeShippedException(Guid orderId, string message)
        : base(OrderFulfillmentErrorCodes.OrderCannotBeShipped, message)
        => OrderId = orderId;
}

public sealed class OrderCannotBeDeliveredException : OrderFulfillmentException
{
    public Guid OrderId { get; }

    public OrderCannotBeDeliveredException(Guid orderId, string message)
        : base(OrderFulfillmentErrorCodes.OrderCannotBeDelivered, message)
        => OrderId = orderId;
}

public sealed class OrderMustBeShippedBeforeDeliveredException : OrderFulfillmentException
{
    public Guid OrderId { get; }

    public OrderMustBeShippedBeforeDeliveredException(Guid orderId)
        : base(
            OrderFulfillmentErrorCodes.OrderMustBeShippedBeforeDelivered,
            "O pedido precisa estar marcado como enviado antes de ser entregue.")
        => OrderId = orderId;
}

public sealed class OrderNoteTooLongException : OrderFulfillmentException
{
    public string Field { get; }

    public OrderNoteTooLongException(string code, string field, string message)
        : base(code, message)
        => Field = field;
}

public sealed class TrackingCodeTooLongException : OrderFulfillmentException
{
    public TrackingCodeTooLongException()
        : base(
            OrderFulfillmentErrorCodes.TrackingCodeTooLong,
            "O código de rastreio deve ter no máximo 120 caracteres.")
    {
    }
}

public sealed class OrderMustBePaidBeforeStockConfirmationException : OrderFulfillmentException
{
    public Guid OrderId { get; }

    public OrderMustBePaidBeforeStockConfirmationException(Guid orderId)
        : base(
            OrderFulfillmentErrorCodes.OrderMustBePaidBeforeStockConfirmation,
            "O pagamento precisa estar aprovado antes de confirmar o estoque.")
        => OrderId = orderId;
}

public sealed class OrderStockConfirmationRequiredException : OrderFulfillmentException
{
    public Guid OrderId { get; }

    public OrderStockConfirmationRequiredException(Guid orderId)
        : base(
            OrderFulfillmentErrorCodes.OrderStockConfirmationRequired,
            "Confirme o estoque antes de marcar o pedido como separado.")
        => OrderId = orderId;
}

public sealed class OrderCannotConfirmStockAfterDeliveredException : OrderFulfillmentException
{
    public Guid OrderId { get; }

    public OrderCannotConfirmStockAfterDeliveredException(Guid orderId)
        : base(
            OrderFulfillmentErrorCodes.OrderCannotConfirmStockAfterDelivered,
            "Este pedido já foi entregue e não pode ter o estoque confirmado.")
        => OrderId = orderId;
}
