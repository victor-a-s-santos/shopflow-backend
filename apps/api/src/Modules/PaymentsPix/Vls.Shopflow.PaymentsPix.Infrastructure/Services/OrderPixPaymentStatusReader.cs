using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.Orders.Application.Interfaces;
using Vls.Shopflow.PaymentsPix.Infrastructure;

namespace Vls.Shopflow.PaymentsPix.Infrastructure.Services;

public sealed class OrderPixPaymentStatusReader(PaymentsPixDbContext db) : IOrderPixPaymentStatusReader
{
    public async Task<OrderPixPaymentStatusSnapshot?> GetLatestByOrderIdAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var payment = await db.PixPayments
            .AsNoTracking()
            .Where(p => p.OrderId == orderId)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (payment is null)
            return null;

        return new OrderPixPaymentStatusSnapshot(
            payment.Status.ToString(),
            payment.Provider.ToString(),
            payment.Amount,
            payment.ExpiresAt,
            payment.PaidAt,
            payment.ProviderUpdatedAt ?? payment.PaidAt ?? payment.CreatedAt);
    }
}
