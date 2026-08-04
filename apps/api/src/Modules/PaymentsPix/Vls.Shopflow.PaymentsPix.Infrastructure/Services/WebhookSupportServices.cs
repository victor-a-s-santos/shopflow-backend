using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.CartCheckout.Infrastructure;
using Vls.Shopflow.Inventory.Application.Repositories;
using Vls.Shopflow.Orders.Domain.Enums;
using Vls.Shopflow.Orders.Infrastructure;
using Vls.Shopflow.PaymentsPix.Application.Interfaces;

namespace Vls.Shopflow.PaymentsPix.Infrastructure.Services;

public sealed class OrderPaidWriter(OrdersDbContext ordersDb) : IOrderPaidWriter
{
    public async Task<OrderPaidWriteResult> GetAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await ordersDb.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order is null)
            return new OrderPaidWriteResult(false, false, false, null, null);

        return ToResult(order, alreadyPaid: order.Status == OrderStatus.Paid, markedPaid: false);
    }

    public async Task<OrderPaidWriteResult> MarkAsPaidAsync(
        Guid orderId,
        DateTimeOffset paidAt,
        CancellationToken cancellationToken)
    {
        var order = await ordersDb.Orders.FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order is null)
            return new OrderPaidWriteResult(false, false, false, null, null);

        if (order.Status == OrderStatus.Paid)
            return ToResult(order, alreadyPaid: true, markedPaid: false);

        if (order.Status != OrderStatus.PendingPayment)
            return ToResult(order, alreadyPaid: false, markedPaid: false);

        order.MarkAsPaid(paidAt);
        await ordersDb.SaveChangesAsync(cancellationToken);

        return ToResult(order, alreadyPaid: false, markedPaid: true);
    }

    private static OrderPaidWriteResult ToResult(
        Vls.Shopflow.Orders.Domain.Entities.Order order,
        bool alreadyPaid,
        bool markedPaid)
        => new(
            true,
            alreadyPaid,
            markedPaid,
            order.Status.ToString(),
            order.CheckoutSessionId,
            order.OrderNumber,
            order.CustomerEmail,
            order.CustomerFullName,
            order.Total,
            order.CustomerUserId,
            order.PreferredDeliveryMethod?.ToString(),
            order.PreferredDeliveryDate);
}

public sealed class CheckoutReservationIdsReader(CartCheckoutDbContext cartCheckoutDb)
    : ICheckoutReservationIdsReader
{
    public async Task<IReadOnlyList<Guid>> GetReservationIdsByCheckoutSessionAsync(
        Guid checkoutSessionId,
        CancellationToken cancellationToken)
    {
        var session = await cartCheckoutDb.CheckoutSessions
            .AsNoTracking()
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == checkoutSessionId, cancellationToken);

        if (session is null)
            return [];

        return session.Items
            .Select(i => i.InventoryReservationId)
            .Distinct()
            .ToList();
    }
}

public sealed class InventoryReservationConfirmer(IInventoryAtomicOperations atomicOperations)
    : IInventoryReservationConfirmer
{
    public Task ConfirmAsync(Guid reservationId, CancellationToken cancellationToken)
        => atomicOperations.ConfirmReservationAsync(reservationId, cancellationToken);
}
