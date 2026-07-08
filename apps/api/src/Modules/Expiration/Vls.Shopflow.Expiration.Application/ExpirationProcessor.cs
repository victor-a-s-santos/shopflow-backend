using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vls.Shopflow.CartCheckout.Application.Interfaces;
using Vls.Shopflow.CartCheckout.Application.Repositories;
using Vls.Shopflow.CartCheckout.Domain.Entities;
using Vls.Shopflow.CartCheckout.Domain.Enums;
using Vls.Shopflow.Expiration.Application.Interfaces;
using Vls.Shopflow.Expiration.Application.Options;
using Vls.Shopflow.Inventory.Domain.Exceptions;
using Vls.Shopflow.Orders.Application.Repositories;
using Vls.Shopflow.Orders.Domain.Entities;
using Vls.Shopflow.Orders.Domain.Enums;
using Vls.Shopflow.PaymentsPix.Application.Repositories;
using Vls.Shopflow.PaymentsPix.Domain.Entities;
using Vls.Shopflow.PaymentsPix.Domain.Enums;

namespace Vls.Shopflow.Expiration.Application;

public sealed class ExpirationProcessor(
    ICheckoutSessionRepository checkoutSessionRepository,
    IOrderRepository orderRepository,
    IPixPaymentRepository pixPaymentRepository,
    IInventoryReservationService inventoryReservation,
    ICartCheckoutUnitOfWork cartCheckoutUnitOfWork,
    IOrdersUnitOfWork ordersUnitOfWork,
    IPaymentsPixUnitOfWork paymentsPixUnitOfWork,
    IExpirationRecoveryReader recoveryReader,
    IOptions<ExpirationWorkerOptions> options,
    ILogger<ExpirationProcessor> logger) : IExpirationProcessor
{
    public async Task<ExpirationBatchResult> ProcessAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var result = new ExpirationBatchResult();
        var now = DateTimeOffset.UtcNow;
        var pixCreatedBefore = now.AddMinutes(-settings.PixPaymentTtlMinutes);

        var expiredSessions = await checkoutSessionRepository.GetExpiredPendingBatchAsync(
            now,
            settings.BatchSize,
            cancellationToken);

        logger.LogInformation(
            "Expiration batch started with {CheckoutSessionCount} expired checkout session candidates",
            expiredSessions.Count);

        foreach (var session in expiredSessions)
        {
            result.Processed++;
            try
            {
                await ExpireCheckoutSessionFlowAsync(session, now, result, cancellationToken);
            }
            catch (Exception ex)
            {
                result.Failures++;
                logger.LogError(ex, "Failed to expire checkout session {CheckoutSessionId}", session.Id);
            }
        }

        var expiredPixPayments = await pixPaymentRepository.GetExpiredPendingBatchAsync(
            now,
            pixCreatedBefore,
            settings.BatchSize,
            cancellationToken);

        foreach (var pixPayment in expiredPixPayments)
        {
            result.Processed++;
            try
            {
                await ExpirePixPaymentFlowAsync(pixPayment, now, result, cancellationToken);
            }
            catch (Exception ex)
            {
                result.Failures++;
                logger.LogError(ex, "Failed to expire Pix payment {PaymentId}", pixPayment.Id);
            }
        }

        var orphanOrders = await recoveryReader.GetOrphanPendingOrdersBatchAsync(
            settings.BatchSize,
            cancellationToken);

        foreach (var orphan in orphanOrders)
        {
            result.Processed++;
            try
            {
                await ExpireOrphanOrderFlowAsync(orphan, now, result, cancellationToken);
            }
            catch (Exception ex)
            {
                result.Failures++;
                logger.LogError(ex, "Failed to recover orphan order {OrderId}", orphan.OrderId);
            }
        }

        logger.LogInformation(
            "Expiration batch completed. Sessions={Sessions}, Orders={Orders}, Pix={Pix}, Reservations={Reservations}, Failures={Failures}",
            result.ExpiredCheckoutSessions,
            result.ExpiredOrders,
            result.ExpiredPixPayments,
            result.CanceledReservations,
            result.Failures);

        return result;
    }

    private async Task ExpireCheckoutSessionFlowAsync(
        CheckoutSession session,
        DateTimeOffset now,
        ExpirationBatchResult result,
        CancellationToken cancellationToken)
    {
        if (session.Status != CheckoutSessionStatus.Pending)
            return;

        if (session.ReservationExpiresAt > now)
            return;

        foreach (var item in session.Items)
        {
            if (await TryCancelReservationAsync(item.InventoryReservationId, cancellationToken))
                result.CanceledReservations++;
        }

        session.Expire();
        await cartCheckoutUnitOfWork.SaveChangesAsync(cancellationToken);
        result.ExpiredCheckoutSessions++;

        logger.LogInformation("Expired checkout session {CheckoutSessionId}", session.Id);

        var order = await orderRepository.GetPendingPaymentByCheckoutSessionIdAsync(
            session.Id,
            cancellationToken);

        if (order is not null)
            await ExpireOrderAndPixAsync(order, result, cancellationToken);
    }

    private async Task ExpirePixPaymentFlowAsync(
        PixPayment pixPayment,
        DateTimeOffset now,
        ExpirationBatchResult result,
        CancellationToken cancellationToken)
    {
        if (pixPayment.Status != PixPaymentStatus.Pending)
            return;

        if (!IsPixPaymentExpired(pixPayment, now))
            return;

        var order = await orderRepository.GetByIdWithItemsAsync(pixPayment.OrderId, cancellationToken);
        if (order is null)
        {
            pixPayment.Expire();
            await paymentsPixUnitOfWork.SaveChangesAsync(cancellationToken);
            result.ExpiredPixPayments++;
            return;
        }

        if (order.Status == OrderStatus.Paid)
            return;

        var session = await checkoutSessionRepository.GetByIdWithItemsAsync(
            order.CheckoutSessionId,
            cancellationToken);

        if (session is not null && session.Status == CheckoutSessionStatus.Pending)
        {
            foreach (var item in session.Items)
            {
                if (await TryCancelReservationAsync(item.InventoryReservationId, cancellationToken))
                    result.CanceledReservations++;
            }

            session.Expire();
            await cartCheckoutUnitOfWork.SaveChangesAsync(cancellationToken);
            result.ExpiredCheckoutSessions++;
        }

        await ExpireOrderAndPixAsync(order, pixPayment, result, cancellationToken);
    }

    private async Task ExpireOrphanOrderFlowAsync(
        OrphanPendingOrderSnapshot orphan,
        DateTimeOffset now,
        ExpirationBatchResult result,
        CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByIdWithItemsAsync(orphan.OrderId, cancellationToken);
        if (order is null || order.Status != OrderStatus.PendingPayment)
            return;

        var session = await checkoutSessionRepository.GetByIdWithItemsAsync(
            orphan.CheckoutSessionId,
            cancellationToken);

        if (session is not null &&
            session.Status == CheckoutSessionStatus.Pending &&
            session.ReservationExpiresAt <= now)
        {
            foreach (var item in session.Items)
            {
                if (await TryCancelReservationAsync(item.InventoryReservationId, cancellationToken))
                    result.CanceledReservations++;
            }

            session.Expire();
            await cartCheckoutUnitOfWork.SaveChangesAsync(cancellationToken);
            result.ExpiredCheckoutSessions++;
        }

        await ExpireOrderAndPixAsync(order, result, cancellationToken);
    }

    private async Task ExpireOrderAndPixAsync(
        Order order,
        ExpirationBatchResult result,
        CancellationToken cancellationToken)
    {
        var pixPayment = await pixPaymentRepository.GetPendingByOrderIdAsync(order.Id, cancellationToken);
        await ExpireOrderAndPixAsync(order, pixPayment, result, cancellationToken);
    }

    private async Task ExpireOrderAndPixAsync(
        Order order,
        PixPayment? pixPayment,
        ExpirationBatchResult result,
        CancellationToken cancellationToken)
    {
        if (order.Status == OrderStatus.PendingPayment)
        {
            order.Expire();
            await ordersUnitOfWork.SaveChangesAsync(cancellationToken);
            result.ExpiredOrders++;
            logger.LogInformation("Expired order {OrderId}", order.Id);
        }

        if (pixPayment is not null && pixPayment.Status == PixPaymentStatus.Pending)
        {
            pixPayment.Expire();
            await paymentsPixUnitOfWork.SaveChangesAsync(cancellationToken);
            result.ExpiredPixPayments++;
            logger.LogInformation("Expired Pix payment {PaymentId}", pixPayment.Id);
        }
    }

    private bool IsPixPaymentExpired(PixPayment pixPayment, DateTimeOffset now)
    {
        if (pixPayment.ExpiresAt.HasValue)
            return pixPayment.ExpiresAt.Value <= now;

        var ttl = TimeSpan.FromMinutes(options.Value.PixPaymentTtlMinutes);
        return pixPayment.CreatedAt.Add(ttl) <= now;
    }

    private async Task<bool> TryCancelReservationAsync(
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        try
        {
            await inventoryReservation.CancelReservationAsync(reservationId, cancellationToken);
            return true;
        }
        catch (InvalidStockReservationStatusException ex)
        {
            logger.LogWarning(
                ex,
                "Reservation {ReservationId} is not pending; skipping cancel",
                reservationId);
            return false;
        }
        catch (StockReservationNotFoundException ex)
        {
            logger.LogWarning(
                ex,
                "Reservation {ReservationId} was not found; skipping cancel",
                reservationId);
            return false;
        }
    }
}
