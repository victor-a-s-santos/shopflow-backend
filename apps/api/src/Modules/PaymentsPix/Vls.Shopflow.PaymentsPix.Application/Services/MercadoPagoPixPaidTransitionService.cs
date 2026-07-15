using Microsoft.Extensions.Logging;
using Vls.Shopflow.PaymentsPix.Application.Interfaces;
using Vls.Shopflow.PaymentsPix.Application.Repositories;
using Vls.Shopflow.PaymentsPix.Domain.Entities;
using Vls.Shopflow.PaymentsPix.Domain.Enums;

namespace Vls.Shopflow.PaymentsPix.Application.Services;

public sealed class MercadoPagoPixPaidTransitionService(
    IOrderPaidWriter orderPaidWriter,
    ICheckoutReservationIdsReader reservationIdsReader,
    IInventoryReservationConfirmer reservationConfirmer,
    IPaymentsPixUnitOfWork unitOfWork,
    ILogger<MercadoPagoPixPaidTransitionService> logger)
    : IMercadoPagoPixPaidTransitionService
{
    public async Task<MercadoPagoPixPaidTransitionResult> ApplyPaidAsync(
        PixPayment pixPayment,
        MercadoPagoOrderLookup mpOrder,
        CancellationToken cancellationToken)
    {
        if (pixPayment.Status == PixPaymentStatus.Paid)
        {
            await TryConfirmReservationsForPaidOrderAsync(pixPayment.OrderId, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return new MercadoPagoPixPaidTransitionResult(
                true, "AlreadyPaid", "Pix payment was already Paid.");
        }

        if (pixPayment.Status != PixPaymentStatus.Pending)
        {
            logger.LogError(
                "Cannot mark PixPayment {PixPaymentId} as Paid from status {Status}.",
                pixPayment.Id,
                pixPayment.Status);

            return new MercadoPagoPixPaidTransitionResult(
                false,
                "InvalidLocalStatus",
                $"Local Pix payment status is {pixPayment.Status}.");
        }

        var orderProbe = await orderPaidWriter.GetAsync(pixPayment.OrderId, cancellationToken);

        if (!orderProbe.Found)
        {
            return new MercadoPagoPixPaidTransitionResult(
                false, "OrderNotFound", "Order was not found.");
        }

        if (!orderProbe.AlreadyPaid
            && !string.Equals(orderProbe.Status, "PendingPayment", StringComparison.Ordinal))
        {
            logger.LogError(
                "Mercado Pago order accredited but Shopflow Order {OrderId} status is {OrderStatus}; not marking Paid.",
                pixPayment.OrderId,
                orderProbe.Status);

            return new MercadoPagoPixPaidTransitionResult(
                false,
                "OrderNotPayable",
                $"Order status is {orderProbe.Status}; reservation was not confirmed.");
        }

        if (orderProbe.CheckoutSessionId is { } checkoutSessionId)
        {
            var reservationIds = await reservationIdsReader.GetReservationIdsByCheckoutSessionAsync(
                checkoutSessionId,
                cancellationToken);

            foreach (var reservationId in reservationIds)
            {
                try
                {
                    await reservationConfirmer.ConfirmAsync(reservationId, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(
                        ex,
                        "Failed to confirm reservation {ReservationId} for order {OrderId}. Aborting Paid transition.",
                        reservationId,
                        pixPayment.OrderId);

                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    return new MercadoPagoPixPaidTransitionResult(
                        false,
                        "ReservationConfirmFailed",
                        "Could not confirm inventory reservation for approved payment.");
                }
            }
        }

        var approvedAt = mpOrder.LastUpdatedDate ?? DateTimeOffset.UtcNow;

        // Order and PixPayment live in separate DbContexts. Persist Order.Paid first.
        var orderWrite = await orderPaidWriter.MarkAsPaidAsync(
            pixPayment.OrderId,
            approvedAt,
            cancellationToken);

        if (!orderWrite.Found || (!orderWrite.AlreadyPaid && !orderWrite.MarkedPaid))
        {
            logger.LogError(
                "Mercado Pago accredited but Order {OrderId} could not be marked Paid (status={Status}); PixPayment {PixPaymentId} left Pending.",
                pixPayment.OrderId,
                orderWrite.Status,
                pixPayment.Id);

            await unitOfWork.SaveChangesAsync(cancellationToken);
            return new MercadoPagoPixPaidTransitionResult(
                false,
                "OrderMarkPaidFailed",
                "Order could not be marked Paid; Pix payment was left Pending.");
        }

        pixPayment.MarkAsPaid(
            mpOrder.Status,
            mpOrder.StatusDetail,
            mpOrder.TransactionStatus,
            mpOrder.TransactionStatusDetail,
            approvedAt,
            mpOrder.Id,
            mpOrder.TransactionId);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new MercadoPagoPixPaidTransitionResult(
            true,
            "Paid",
            "Pix payment and order marked Paid; reservations confirmed.");
    }

    private async Task TryConfirmReservationsForPaidOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var orderResult = await orderPaidWriter.GetAsync(orderId, cancellationToken);
        if (orderResult.CheckoutSessionId is not { } checkoutSessionId)
            return;

        var reservationIds = await reservationIdsReader.GetReservationIdsByCheckoutSessionAsync(
            checkoutSessionId,
            cancellationToken);

        foreach (var reservationId in reservationIds)
        {
            try
            {
                await reservationConfirmer.ConfirmAsync(reservationId, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Idempotent confirm skipped/failed for reservation {ReservationId} on order {OrderId}.",
                    reservationId,
                    orderId);
            }
        }
    }
}
