using Vls.Shopflow.Orders.Domain.Entities;
using Vls.Shopflow.Orders.Domain.Enums;

namespace Vls.Shopflow.Orders.Application.Repositories;

public interface IOrderEmailIntentRepository
{
    Task<bool> ExistsByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    Task EnsurePendingAsync(OrderEmailIntent intent, CancellationToken cancellationToken = default);

    Task ExecutePendingBatchAsync(
        int batchSize,
        Func<OrderEmailIntent, CancellationToken, Task<bool>> dispatch,
        CancellationToken cancellationToken = default);

    Task<int> RepairMissingIntentsAsync(int batchSize, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public static class OrderEmailIntentFactory
{
    public static OrderEmailIntent PendingFromOrder(
        Order order,
        OrderEmailIntentType type,
        string? guestAccessToken = null)
        => OrderEmailIntent.CreatePending(
            order.Id,
            type,
            Models.OrderEmailIntentPayloadJson.Serialize(
                Models.OrderEmailIntentPayloadJson.FromOrder(order, guestAccessToken)));
}
