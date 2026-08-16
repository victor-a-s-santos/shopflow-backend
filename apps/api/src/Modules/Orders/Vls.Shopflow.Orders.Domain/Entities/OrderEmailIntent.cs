using Vls.Shopflow.BuildingBlocks.Domain.Entities;
using Vls.Shopflow.Orders.Domain.Enums;

namespace Vls.Shopflow.Orders.Domain.Entities;

public sealed class OrderEmailIntent : Entity<Guid>
{
    public const int MaxIdempotencyKeyLength = 200;
    public const int MaxTypeLength = 64;
    public const int MaxStatusLength = 32;

    public Guid OrderId { get; private set; }
    public OrderEmailIntentType Type { get; private set; }
    public string IdempotencyKey { get; private set; } = default!;
    public string PayloadJson { get; private set; } = default!;
    public OrderEmailIntentStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? DispatchedAt { get; private set; }

    private OrderEmailIntent() { }

    public static string KeyFor(Guid orderId, OrderEmailIntentType type)
        => type switch
        {
            OrderEmailIntentType.OrderCreated => $"order:{orderId:D}:created",
            OrderEmailIntentType.PaymentConfirmed => $"order:{orderId:D}:paid",
            OrderEmailIntentType.OrderShipped => $"order:{orderId:D}:shipped",
            OrderEmailIntentType.OrderDelivered => $"order:{orderId:D}:delivered",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown order email intent type.")
        };

    public static OrderEmailIntent CreatePending(
        Guid orderId,
        OrderEmailIntentType type,
        string payloadJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);

        var now = DateTimeOffset.UtcNow;
        return new OrderEmailIntent
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Type = type,
            IdempotencyKey = KeyFor(orderId, type),
            PayloadJson = payloadJson,
            Status = OrderEmailIntentStatus.Pending,
            CreatedAt = now
        };
    }

    public void MarkDispatched(DateTimeOffset? dispatchedAt = null)
    {
        Status = OrderEmailIntentStatus.Dispatched;
        DispatchedAt = dispatchedAt ?? DateTimeOffset.UtcNow;
    }
}
