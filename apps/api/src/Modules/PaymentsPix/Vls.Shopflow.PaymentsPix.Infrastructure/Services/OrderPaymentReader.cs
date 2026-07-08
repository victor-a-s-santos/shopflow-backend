using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.Orders.Infrastructure;
using Vls.Shopflow.PaymentsPix.Application.Interfaces;

namespace Vls.Shopflow.PaymentsPix.Infrastructure.Services;

public sealed class OrderPaymentReader(OrdersDbContext ordersDb) : IOrderPaymentReader
{
    public async Task<OrderPaymentSnapshot?> GetByIdAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var order = await ordersDb.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order is null)
            return null;

        return new OrderPaymentSnapshot(
            order.Id,
            order.Status.ToString(),
            order.Total,
            order.CustomerFullName,
            order.CustomerEmail);
    }
}
