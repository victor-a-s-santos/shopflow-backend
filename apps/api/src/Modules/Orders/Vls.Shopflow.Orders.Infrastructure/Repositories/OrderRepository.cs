using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.Orders.Application.Repositories;
using Vls.Shopflow.Orders.Domain.Entities;
using Vls.Shopflow.Orders.Domain.Enums;
using Vls.Shopflow.Orders.Domain.Services;

namespace Vls.Shopflow.Orders.Infrastructure.Repositories;

public sealed class OrderRepository(OrdersDbContext db) : IOrderRepository
{
    public async Task AddAsync(Order order, CancellationToken cancellationToken)
        => await db.Orders.AddAsync(order, cancellationToken);

    public Task<Order?> GetByIdWithItemsAsync(Guid orderId, CancellationToken cancellationToken)
        => db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

    public Task<Order?> GetByOrderNumberWithItemsAsync(long orderNumber, CancellationToken cancellationToken)
        => db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber, cancellationToken);

    public Task<Order?> GetByCheckoutSessionIdWithItemsAsync(
        Guid checkoutSessionId,
        CancellationToken cancellationToken)
        => db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.CheckoutSessionId == checkoutSessionId, cancellationToken);

    public Task<bool> ExistsByCheckoutSessionIdAsync(Guid checkoutSessionId, CancellationToken cancellationToken)
        => db.Orders.AnyAsync(o => o.CheckoutSessionId == checkoutSessionId, cancellationToken);

    public Task<Order?> GetPendingPaymentByCheckoutSessionIdAsync(
        Guid checkoutSessionId,
        CancellationToken cancellationToken)
        => db.Orders.FirstOrDefaultAsync(
            o => o.CheckoutSessionId == checkoutSessionId &&
                 o.Status == OrderStatus.PendingPayment,
            cancellationToken);

    public async Task<IReadOnlyList<Order>> GetByIdsWithItemsAsync(
        IReadOnlyCollection<Guid> orderIds,
        CancellationToken cancellationToken)
    {
        if (orderIds.Count == 0)
            return [];

        return await db.Orders
            .Include(o => o.Items)
            .Where(o => orderIds.Contains(o.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Order>> FindEligibleGroupingCandidatesAsync(
        Guid? customerUserId,
        string? emailNormalized,
        string? phoneNormalized,
        CancellationToken cancellationToken)
    {
        var query = db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.Status == OrderStatus.Paid
                        && o.FulfillmentStatus == FulfillmentStatus.AwaitingShipment);

        if (customerUserId is not null)
        {
            return await query
                .Where(o => o.CustomerUserId == customerUserId)
                .OrderBy(o => o.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        if (emailNormalized is null || phoneNormalized is null)
            return [];

        var byEmail = await query
            .Where(o => o.CustomerUserId == null
                        && o.CustomerEmail.ToLower() == emailNormalized)
            .OrderBy(o => o.CreatedAt)
            .ToListAsync(cancellationToken);

        return byEmail
            .Where(o => CustomerContactNormalizer.NormalizePhone(o.CustomerPhone) == phoneNormalized)
            .ToList();
    }
}
