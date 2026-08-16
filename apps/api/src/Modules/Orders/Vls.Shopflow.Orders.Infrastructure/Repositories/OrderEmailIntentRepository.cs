using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.Orders.Application.Repositories;
using Vls.Shopflow.Orders.Domain.Entities;
using Vls.Shopflow.Orders.Domain.Enums;

namespace Vls.Shopflow.Orders.Infrastructure.Repositories;

public sealed class OrderEmailIntentRepository(OrdersDbContext db) : IOrderEmailIntentRepository
{
    public Task<bool> ExistsByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
        => db.EmailIntents.AnyAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);

    public async Task EnsurePendingAsync(OrderEmailIntent intent, CancellationToken cancellationToken = default)
    {
        if (db.EmailIntents.Local.Any(x => x.IdempotencyKey == intent.IdempotencyKey))
            return;

        if (await db.EmailIntents.AnyAsync(x => x.IdempotencyKey == intent.IdempotencyKey, cancellationToken))
            return;

        await db.EmailIntents.AddAsync(intent, cancellationToken);
    }

    public async Task ExecutePendingBatchAsync(
        int batchSize,
        Func<OrderEmailIntent, CancellationToken, Task<bool>> dispatch,
        CancellationToken cancellationToken = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var pending = nameof(OrderEmailIntentStatus.Pending);
        var batch = await db.EmailIntents
            .FromSqlInterpolated($"""
                SELECT * FROM orders.email_intents
                WHERE "Status" = {pending}
                ORDER BY "CreatedAt"
                LIMIT {batchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);

        foreach (var intent in batch)
        {
            try
            {
                if (await dispatch(intent, cancellationToken))
                    intent.MarkDispatched();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // Leave Pending for a later retry. Do not log payload/token.
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

    public async Task<int> RepairMissingIntentsAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var repaired = 0;
        repaired += await RepairMissingAsync(
            OrderStatus.Paid,
            fulfillmentStatuses: null,
            OrderEmailIntentType.PaymentConfirmed,
            batchSize - repaired,
            cancellationToken);

        if (repaired >= batchSize)
            return repaired;

        repaired += await RepairMissingAsync(
            OrderStatus.Paid,
            [FulfillmentStatus.Shipped, FulfillmentStatus.Delivered],
            OrderEmailIntentType.OrderShipped,
            batchSize - repaired,
            cancellationToken);

        if (repaired >= batchSize)
            return repaired;

        repaired += await RepairMissingAsync(
            OrderStatus.Paid,
            [FulfillmentStatus.Delivered],
            OrderEmailIntentType.OrderDelivered,
            batchSize - repaired,
            cancellationToken);

        return repaired;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => db.SaveChangesAsync(cancellationToken);

    private async Task<int> RepairMissingAsync(
        OrderStatus orderStatus,
        FulfillmentStatus[]? fulfillmentStatuses,
        OrderEmailIntentType type,
        int remaining,
        CancellationToken cancellationToken)
    {
        if (remaining <= 0)
            return 0;

        var query = db.Orders.Where(o => o.Status == orderStatus);
        if (fulfillmentStatuses is { Length: > 0 })
            query = query.Where(o => fulfillmentStatuses.Contains(o.FulfillmentStatus));

        var missing = await query
            .Where(o => !db.EmailIntents.Any(i => i.OrderId == o.Id && i.Type == type))
            .OrderBy(o => o.CreatedAt)
            .Take(remaining)
            .ToListAsync(cancellationToken);

        foreach (var order in missing)
            await EnsurePendingAsync(OrderEmailIntentFactory.PendingFromOrder(order, type), cancellationToken);

        return missing.Count;
    }
}
