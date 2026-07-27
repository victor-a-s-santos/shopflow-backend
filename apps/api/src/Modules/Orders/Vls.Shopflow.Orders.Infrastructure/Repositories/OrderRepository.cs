using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.Orders.Application.Repositories;
using Vls.Shopflow.Orders.Domain.Entities;

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
                 o.Status == Domain.Enums.OrderStatus.PendingPayment,
            cancellationToken);
}
