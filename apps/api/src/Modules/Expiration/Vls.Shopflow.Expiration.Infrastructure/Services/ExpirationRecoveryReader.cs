using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.CartCheckout.Domain.Enums;
using Vls.Shopflow.CartCheckout.Infrastructure;
using Vls.Shopflow.Expiration.Application.Interfaces;
using Vls.Shopflow.Orders.Domain.Enums;
using Vls.Shopflow.Orders.Infrastructure;

namespace Vls.Shopflow.Expiration.Infrastructure.Services;

public sealed class ExpirationRecoveryReader(
    OrdersDbContext ordersDb,
    CartCheckoutDbContext cartCheckoutDb) : IExpirationRecoveryReader
{
    public async Task<IReadOnlyList<OrphanPendingOrderSnapshot>> GetOrphanPendingOrdersBatchAsync(
        int batchSize,
        CancellationToken cancellationToken)
    {
        var pendingOrders = await ordersDb.Orders
            .AsNoTracking()
            .Where(o => o.Status == OrderStatus.PendingPayment)
            .OrderBy(o => o.CreatedAt)
            .Take(batchSize * 3)
            .Select(o => new { o.Id, o.CheckoutSessionId })
            .ToListAsync(cancellationToken);

        if (pendingOrders.Count == 0)
            return Array.Empty<OrphanPendingOrderSnapshot>();

        var sessionIds = pendingOrders.Select(o => o.CheckoutSessionId).Distinct().ToList();

        var finalizedSessionIds = await cartCheckoutDb.CheckoutSessions
            .AsNoTracking()
            .Where(s =>
                sessionIds.Contains(s.Id) &&
                (s.Status == CheckoutSessionStatus.Expired ||
                 s.Status == CheckoutSessionStatus.Canceled))
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        var finalizedSet = finalizedSessionIds.ToHashSet();

        return pendingOrders
            .Where(o => finalizedSet.Contains(o.CheckoutSessionId))
            .Take(batchSize)
            .Select(o => new OrphanPendingOrderSnapshot(o.Id, o.CheckoutSessionId))
            .ToList();
    }
}
