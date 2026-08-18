using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vls.Shopflow.PaymentsPix.Application.Interfaces;
using Vls.Shopflow.PaymentsPix.Application.Options;
using Vls.Shopflow.PaymentsPix.Application.Repositories;
using Vls.Shopflow.PaymentsPix.Domain.Entities;
using Vls.Shopflow.PaymentsPix.Domain.Enums;

namespace Vls.Shopflow.PaymentsPix.Application.Services;

/// <summary>
/// Fallback: poll pending Mercado Pago Pix via GET /v1/orders. Does not replace webhooks.
/// </summary>
public sealed class MercadoPagoPixReconciliationProcessor(
    IOptions<MercadoPagoReconciliationOptions> reconciliationOptions,
    IPixPaymentRepository paymentRepository,
    IMercadoPagoOrderClient orderClient,
    IMercadoPagoPixPaidTransitionService paidTransition,
    IPaymentsPixUnitOfWork unitOfWork,
    ILogger<MercadoPagoPixReconciliationProcessor> logger)
    : IMercadoPagoPixReconciliationProcessor
{
    public async Task<MercadoPagoPixReconciliationBatchResult> ProcessAsync(CancellationToken cancellationToken)
    {
        var settings = reconciliationOptions.Value;
        if (!settings.Enabled)
        {
            return new MercadoPagoPixReconciliationBatchResult(0, 0, 0, 0, 0, 0, 0);
        }

        var batchSize = settings.BatchSize <= 0 ? 20 : settings.BatchSize;
        var maxAgeMinutes = settings.MaxAgeMinutes <= 0 ? 180 : settings.MaxAgeMinutes;
        var createdAfterUtc = DateTimeOffset.UtcNow.AddMinutes(-maxAgeMinutes);

        var candidates = await paymentRepository.GetPendingMercadoPagoForReconciliationBatchAsync(
            createdAfterUtc,
            batchSize,
            cancellationToken);

        var markedPaid = 0;
        var stillPending = 0;
        var terminalUpdated = 0;
        var lookupsSkipped = 0;
        var failures = 0;
        var processed = 0;

        logger.LogInformation(
            "Mercado Pago Pix reconciliation batch started. Candidates={Candidates} BatchSize={BatchSize} MaxAgeMinutes={MaxAgeMinutes}",
            candidates.Count,
            batchSize,
            maxAgeMinutes);

        foreach (var pixPayment in candidates)
        {
            processed++;
            try
            {
                var outcome = await ReconcileOneAsync(pixPayment, cancellationToken);
                switch (outcome)
                {
                    case "Paid" or "AlreadyPaid":
                        markedPaid++;
                        break;
                    case "Pending":
                        stillPending++;
                        break;
                    case "Failed" or "Canceled" or "Expired":
                        terminalUpdated++;
                        break;
                    case "LookupSkipped":
                        lookupsSkipped++;
                        break;
                    default:
                        // Unhandled / mismatch — counted as processed, not failure
                        break;
                }
            }
            catch (Exception ex)
            {
                failures++;
                logger.LogError(
                    ex,
                    "Mercado Pago reconciliation failed for PixPayment {PixPaymentId} OrderId={OrderId}",
                    pixPayment.Id,
                    pixPayment.OrderId);
            }
        }

        logger.LogInformation(
            "Mercado Pago Pix reconciliation batch finished. Processed={Processed} MarkedPaid={MarkedPaid} StillPending={StillPending} Terminal={Terminal} LookupsSkipped={LookupsSkipped} Failures={Failures}",
            processed,
            markedPaid,
            stillPending,
            terminalUpdated,
            lookupsSkipped,
            failures);

        return new MercadoPagoPixReconciliationBatchResult(
            candidates.Count,
            processed,
            markedPaid,
            stillPending,
            terminalUpdated,
            lookupsSkipped,
            failures);
    }

    private async Task<string> ReconcileOneAsync(PixPayment pixPayment, CancellationToken cancellationToken)
    {
        if (pixPayment.Status != PixPaymentStatus.Pending
            || pixPayment.Provider != PixPaymentProviderType.MercadoPago
            || string.IsNullOrWhiteSpace(pixPayment.ProviderOrderId))
        {
            return "Skipped";
        }

        var providerOrderId = pixPayment.ProviderOrderId.Trim();
        var lookup = await orderClient.GetOrderAsync(providerOrderId, cancellationToken);

        switch (lookup.Status)
        {
            case MercadoPagoOrderLookupStatus.TransientFailure:
                logger.LogWarning(
                    "Mercado Pago reconciliation GET transient failure for {MaskedOrderId}. Will retry next round. Http={HttpStatusCode}",
                    MaskProviderOrderId(providerOrderId),
                    lookup.HttpStatusCode);
                return "LookupSkipped";

            case MercadoPagoOrderLookupStatus.NotFound:
            case MercadoPagoOrderLookupStatus.BadRequest:
                logger.LogWarning(
                    "Mercado Pago reconciliation GET {LookupStatus} for {MaskedOrderId}. Skipping item. Http={HttpStatusCode}",
                    lookup.Status,
                    MaskProviderOrderId(providerOrderId),
                    lookup.HttpStatusCode);
                return "LookupSkipped";

            case MercadoPagoOrderLookupStatus.Unauthorized:
                logger.LogError(
                    "Mercado Pago reconciliation GET unauthorized for {MaskedOrderId}. Check AccessToken. Http={HttpStatusCode}",
                    MaskProviderOrderId(providerOrderId),
                    lookup.HttpStatusCode);
                return "LookupSkipped";

            case MercadoPagoOrderLookupStatus.Found:
                break;

            default:
                return "LookupSkipped";
        }

        var mpOrder = lookup.Order
                      ?? throw new InvalidOperationException("Found lookup without order payload.");

        if (!MercadoPagoOrderStatusRules.IsPixPaymentMethod(mpOrder))
        {
            logger.LogWarning(
                "Mercado Pago reconciliation ignored non-Pix method {PaymentMethodId}/{PaymentMethodType} for {MaskedOrderId}",
                mpOrder.PaymentMethodId,
                mpOrder.PaymentMethodType,
                MaskProviderOrderId(providerOrderId));
            return "Unhandled";
        }

        if (!MercadoPagoOrderStatusRules.AmountsMatch(pixPayment.Amount, mpOrder.TotalAmount))
        {
            logger.LogError(
                "Mercado Pago reconciliation amount mismatch. PixPaymentId={PixPaymentId} Local={LocalAmount} ProviderTotal={ProviderTotal}",
                pixPayment.Id,
                pixPayment.Amount,
                mpOrder.TotalAmount);
            return "AmountMismatch";
        }

        if (!MercadoPagoOrderStatusRules.ExternalReferenceMatches(pixPayment.OrderId, mpOrder.ExternalReference))
        {
            logger.LogError(
                "Mercado Pago reconciliation external_reference mismatch. PixPaymentId={PixPaymentId} OrderId={OrderId}",
                pixPayment.Id,
                pixPayment.OrderId);
            return "ExternalReferenceMismatch";
        }

        pixPayment.SyncProviderIds(mpOrder.Id, mpOrder.TransactionId);

        if (MercadoPagoOrderStatusRules.IsPaid(mpOrder))
        {
            var paid = await paidTransition.ApplyPaidAsync(pixPayment, mpOrder, cancellationToken);
            logger.LogInformation(
                "Mercado Pago reconciliation Paid transition. PixPaymentId={PixPaymentId} Outcome={Outcome}",
                pixPayment.Id,
                paid.Outcome);
            return paid.Outcome;
        }

        if (MercadoPagoOrderStatusRules.IsPending(mpOrder))
        {
            pixPayment.UpdateProviderStatus(
                mpOrder.Status,
                mpOrder.StatusDetail,
                mpOrder.TransactionStatus,
                mpOrder.TransactionStatusDetail);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return "Pending";
        }

        var status = (mpOrder.Status ?? string.Empty).Trim().ToLowerInvariant();
        if (status is "failed")
        {
            pixPayment.MarkAsFailed(
                mpOrder.Status,
                mpOrder.StatusDetail,
                "Provider order failed.",
                mpOrder.TransactionStatus,
                mpOrder.TransactionStatusDetail);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return "Failed";
        }

        if (status is "canceled" or "cancelled")
        {
            pixPayment.MarkAsCanceled(
                mpOrder.Status,
                mpOrder.StatusDetail,
                "Provider order canceled.",
                mpOrder.TransactionStatus,
                mpOrder.TransactionStatusDetail);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return "Canceled";
        }

        if (status == "expired")
        {
            if (pixPayment.Status == PixPaymentStatus.Pending)
                pixPayment.Expire();

            pixPayment.UpdateProviderStatus(
                mpOrder.Status,
                mpOrder.StatusDetail,
                mpOrder.TransactionStatus,
                mpOrder.TransactionStatusDetail);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return "Expired";
        }

        pixPayment.UpdateProviderStatus(
            mpOrder.Status,
            mpOrder.StatusDetail,
            mpOrder.TransactionStatus,
            mpOrder.TransactionStatusDetail);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogWarning(
            "Mercado Pago reconciliation unhandled status {Status}/{StatusDetail} for {MaskedOrderId}",
            mpOrder.Status,
            mpOrder.StatusDetail,
            MaskProviderOrderId(providerOrderId));
        return "Unhandled";
    }

    private static string MaskProviderOrderId(string providerOrderId)
    {
        var trimmed = providerOrderId.Trim();
        if (trimmed.Length <= 8)
            return "***";

        return $"{trimmed[..4]}…{trimmed[^4..]}";
    }
}
