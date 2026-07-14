using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vls.Shopflow.PaymentsPix.Application.Commands;
using Vls.Shopflow.PaymentsPix.Application.Interfaces;
using Vls.Shopflow.PaymentsPix.Application.Options;
using Vls.Shopflow.PaymentsPix.Application.Repositories;
using Vls.Shopflow.PaymentsPix.Domain.Entities;
using Vls.Shopflow.PaymentsPix.Domain.Enums;

namespace Vls.Shopflow.PaymentsPix.Application.CommandHandlers;

public sealed class ProcessMercadoPagoPixWebhookCommandHandler(
    IOptions<MercadoPagoOptions> mercadoPagoOptions,
    IMercadoPagoWebhookSignatureValidator signatureValidator,
    IMercadoPagoOrderClient orderClient,
    IPixPaymentRepository paymentRepository,
    IMercadoPagoWebhookEventRepository webhookEventRepository,
    IOrderPaidWriter orderPaidWriter,
    ICheckoutReservationIdsReader reservationIdsReader,
    IInventoryReservationConfirmer reservationConfirmer,
    IPaymentsPixUnitOfWork unitOfWork,
    ILogger<ProcessMercadoPagoPixWebhookCommandHandler> logger)
    : IRequestHandler<ProcessMercadoPagoPixWebhookCommand, ProcessMercadoPagoPixWebhookResult>
{
    private static readonly HashSet<string> PendingStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "created", "processing", "action_required"
    };

    private static readonly HashSet<string> PendingStatusDetails = new(StringComparer.OrdinalIgnoreCase)
    {
        "waiting_payment", "waiting_transfer"
    };

    public async Task<ProcessMercadoPagoPixWebhookResult> Handle(
        ProcessMercadoPagoPixWebhookCommand command,
        CancellationToken cancellationToken)
    {
        var options = mercadoPagoOptions.Value;
        var dataIdFromQuery = Normalize(command.DataIdFromQuery);
        var dataIdFromBody = Normalize(command.DataIdFromBody);

        if (string.IsNullOrWhiteSpace(options.WebhookSecret))
        {
            logger.LogError("Mercado Pago webhook rejected: WebhookSecret is not configured.");
            return new ProcessMercadoPagoPixWebhookResult(
                503,
                "Misconfigured",
                "Mercado Pago webhook secret is not configured.");
        }

        // HMAC uses only query data.id (never body). Missing parts are omitted from the official manifest.
        var signatureResult = signatureValidator.Validate(
            command.XSignature,
            command.XRequestId,
            dataIdFromQuery,
            options.WebhookSecret);

        if (!signatureResult.IsValid)
        {
            LogSignatureFailure(signatureResult, dataIdFromBody);
            return new ProcessMercadoPagoPixWebhookResult(
                401,
                "InvalidSignature",
                "Invalid Mercado Pago webhook signature.");
        }

        // Signature can validate without query data.id (id omitted from manifest), but never mark Paid.
        if (string.IsNullOrWhiteSpace(dataIdFromQuery))
        {
            logger.LogWarning(
                "Mercado Pago webhook signature valid but query data.id missing. HasBodyDataId={HasBodyDataId}. FinalStatus=Ignored",
                !string.IsNullOrWhiteSpace(dataIdFromBody));

            var missingIdEvent = (await GetOrCreateWebhookEventAsync(
                dataIdFromBody ?? "missing-query-data-id",
                command,
                cancellationToken)).Event;

            if (missingIdEvent.ProcessingStatus is not ("Processed" or "Ignored" or "LookupFailed"))
            {
                missingIdEvent.MarkIgnored("MissingQueryDataId: signature validated without query data.id; payment not processed.");
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return new ProcessMercadoPagoPixWebhookResult(
                200,
                "MissingQueryDataId",
                "Webhook signature valid but query data.id is missing; payment not processed.");
        }

        if (!string.IsNullOrWhiteSpace(dataIdFromBody)
            && !string.Equals(dataIdFromQuery, dataIdFromBody, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "Mercado Pago webhook query data.id differs from body data.id. QueryMasked={QueryMasked} BodyMasked={BodyMasked}. FinalStatus=Ignored",
                MaskProviderOrderId(dataIdFromQuery),
                MaskProviderOrderId(dataIdFromBody));

            var (mismatchEvent, mismatchAlready) = await GetOrCreateWebhookEventAsync(
                dataIdFromQuery,
                command,
                cancellationToken);

            if (mismatchAlready is not null)
                return mismatchAlready;

            mismatchEvent.MarkIgnored("DataIdMismatch: query data.id differs from body data.id; payment not processed.");
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return new ProcessMercadoPagoPixWebhookResult(
                200,
                "DataIdMismatch",
                "Query data.id differs from body data.id; payment not processed.");
        }

        var providerOrderId = dataIdFromQuery;
        logger.LogInformation(
            "Mercado Pago webhook received. MaskedOrderId={MaskedOrderId} DataIdSource=query SignatureValid=true LiveMode={LiveMode} Type={Type} Action={Action}",
            MaskProviderOrderId(providerOrderId),
            command.LiveMode,
            command.Type,
            command.Action);

        var (webhookEvent, alreadyResult) = await GetOrCreateWebhookEventAsync(
            providerOrderId,
            command,
            cancellationToken);

        if (alreadyResult is not null)
            return alreadyResult;

        try
        {
            if (!string.IsNullOrWhiteSpace(command.Type)
                && !string.Equals(command.Type, "order", StringComparison.OrdinalIgnoreCase))
            {
                webhookEvent.MarkIgnored($"Webhook type '{command.Type}' ignored; expected order.");
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return new ProcessMercadoPagoPixWebhookResult(
                    200,
                    "IgnoredType",
                    "Webhook type is not order.");
            }

            // Painel simulation often sends generic data.id (e.g. 123456). Real Orders API ids are ORD… / ORDTST….
            if (!IsValidMercadoPagoOrdersApiId(providerOrderId))
            {
                logger.LogWarning(
                    "Mercado Pago webhook ignored: simulator/invalid order id format. MaskedOrderId={MaskedOrderId} FinalStatus=Ignored Outcome=SimulatorEvent",
                    MaskProviderOrderId(providerOrderId));
                webhookEvent.MarkIgnored("SimulatorEvent: not an Orders API id (expected ORD…/ORDTST…).");
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return new ProcessMercadoPagoPixWebhookResult(
                    200,
                    "SimulatorEvent",
                    "Provider order id looks like a panel simulation; payment not processed.");
            }

            var lookup = await orderClient.GetOrderAsync(providerOrderId, cancellationToken);
            logger.LogInformation(
                "Mercado Pago order lookup finished. MaskedOrderId={MaskedOrderId} LookupStatus={LookupStatus} HttpStatus={HttpStatus}",
                MaskProviderOrderId(providerOrderId),
                lookup.Status,
                lookup.HttpStatusCode);

            if (lookup.Status is MercadoPagoOrderLookupStatus.BadRequest or MercadoPagoOrderLookupStatus.NotFound)
            {
                logger.LogWarning(
                    "Mercado Pago webhook lookup failed (non-retryable). MaskedOrderId={MaskedOrderId} LookupStatus={LookupStatus} FinalStatus=LookupFailed",
                    MaskProviderOrderId(providerOrderId),
                    lookup.Status);
                webhookEvent.MarkLookupFailed(lookup.ErrorMessage ?? $"Order lookup {lookup.Status}.");
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return new ProcessMercadoPagoPixWebhookResult(
                    200,
                    "LookupFailed",
                    lookup.ErrorMessage ?? "Order lookup failed at Mercado Pago.");
            }

            if (lookup.Status == MercadoPagoOrderLookupStatus.Unauthorized)
            {
                logger.LogError(
                    "Mercado Pago webhook order lookup unauthorized. Check AccessToken. MaskedOrderId={MaskedOrderId} FinalStatus=Failed",
                    MaskProviderOrderId(providerOrderId));
                webhookEvent.MarkFailed(lookup.ErrorMessage ?? "Mercado Pago AccessToken unauthorized.");
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return new ProcessMercadoPagoPixWebhookResult(
                    503,
                    "MisconfiguredAccessToken",
                    "Mercado Pago Access Token is unauthorized or forbidden.");
            }

            if (lookup.Status == MercadoPagoOrderLookupStatus.TransientFailure)
            {
                // Mark Failed and rethrow so MP can retry (5xx / unexpected HTTP errors).
                throw new InvalidOperationException(
                    lookup.ErrorMessage ?? "Mercado Pago order lookup transient failure.");
            }

            var mpOrder = lookup.Order;
            if (mpOrder is null)
            {
                webhookEvent.MarkLookupFailed("Order lookup returned empty payload.");
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return new ProcessMercadoPagoPixWebhookResult(
                    200,
                    "LookupFailed",
                    "Order lookup returned empty payload.");
            }

            var pixPayment = await ResolvePixPaymentAsync(mpOrder, providerOrderId, cancellationToken);
            if (pixPayment is null)
            {
                webhookEvent.MarkIgnored("No local PixPayment matched provider order.");
                await unitOfWork.SaveChangesAsync(cancellationToken);
                logger.LogWarning(
                    "Mercado Pago webhook order {MaskedOrderId} has no matching local PixPayment. FinalStatus=Ignored",
                    MaskProviderOrderId(providerOrderId));

                return new ProcessMercadoPagoPixWebhookResult(
                    200,
                    "LocalPaymentNotFound",
                    "No local Pix payment matched the provider order.");
            }

            if (pixPayment.Provider != PixPaymentProviderType.MercadoPago)
            {
                webhookEvent.MarkIgnored("Local payment provider is not MercadoPago.");
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return new ProcessMercadoPagoPixWebhookResult(
                    200,
                    "ProviderMismatch",
                    "Local payment provider is not MercadoPago.",
                    pixPayment.Id,
                    pixPayment.OrderId);
            }

            if (!IsPixPaymentMethod(mpOrder))
            {
                webhookEvent.MarkIgnored("payment_method is not pix/bank_transfer.");
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return new ProcessMercadoPagoPixWebhookResult(
                    200,
                    "NotPix",
                    "Provider order is not a Pix payment.",
                    pixPayment.Id,
                    pixPayment.OrderId);
            }

            if (!AmountsMatch(pixPayment.Amount, mpOrder.TotalAmount)
                || (mpOrder.TransactionAmount is { } txAmount && !AmountsMatch(pixPayment.Amount, txAmount)))
            {
                logger.LogError(
                    "Mercado Pago webhook amount mismatch. PixPaymentId={PixPaymentId} Local={LocalAmount} ProviderTotal={ProviderTotal}",
                    pixPayment.Id,
                    pixPayment.Amount,
                    mpOrder.TotalAmount);

                webhookEvent.MarkFailed("Amount mismatch.");
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return new ProcessMercadoPagoPixWebhookResult(
                    200,
                    "AmountMismatch",
                    "Provider order amount does not match local Pix payment.",
                    pixPayment.Id,
                    pixPayment.OrderId);
            }

            if (!ExternalReferenceMatches(pixPayment.OrderId, mpOrder.ExternalReference))
            {
                logger.LogError(
                    "Mercado Pago webhook external_reference mismatch. PixPaymentId={PixPaymentId} OrderId={OrderId} ExternalReference={ExternalReference}",
                    pixPayment.Id,
                    pixPayment.OrderId,
                    mpOrder.ExternalReference);

                webhookEvent.MarkFailed("External reference mismatch.");
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return new ProcessMercadoPagoPixWebhookResult(
                    200,
                    "ExternalReferenceMismatch",
                    "Provider external_reference does not match local order.",
                    pixPayment.Id,
                    pixPayment.OrderId);
            }

            pixPayment.SyncProviderIds(mpOrder.Id, mpOrder.TransactionId);

            if (IsPaid(mpOrder))
            {
                return await ProcessPaidAsync(pixPayment, mpOrder, webhookEvent, cancellationToken);
            }

            if (IsPending(mpOrder))
            {
                pixPayment.UpdateProviderStatus(
                    mpOrder.Status,
                    mpOrder.StatusDetail,
                    mpOrder.TransactionStatus,
                    mpOrder.TransactionStatusDetail);
                webhookEvent.MarkProcessed();
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return new ProcessMercadoPagoPixWebhookResult(
                    200,
                    "Pending",
                    "Provider order is still pending payment.",
                    pixPayment.Id,
                    pixPayment.OrderId);
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
                webhookEvent.MarkProcessed();
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return new ProcessMercadoPagoPixWebhookResult(
                    200, "Failed", "Provider order failed.", pixPayment.Id, pixPayment.OrderId);
            }

            if (status is "canceled" or "cancelled")
            {
                pixPayment.MarkAsCanceled(
                    mpOrder.Status,
                    mpOrder.StatusDetail,
                    "Provider order canceled.",
                    mpOrder.TransactionStatus,
                    mpOrder.TransactionStatusDetail);
                webhookEvent.MarkProcessed();
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return new ProcessMercadoPagoPixWebhookResult(
                    200, "Canceled", "Provider order canceled.", pixPayment.Id, pixPayment.OrderId);
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
                webhookEvent.MarkProcessed();
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return new ProcessMercadoPagoPixWebhookResult(
                    200, "Expired", "Provider order expired.", pixPayment.Id, pixPayment.OrderId);
            }

            if (status is "refunded" or "charged_back")
            {
                logger.LogWarning(
                    "Mercado Pago order {MaskedOrderId} has status {Status}; refund/chargeback not handled in this stage.",
                    MaskProviderOrderId(providerOrderId),
                    mpOrder.Status);
                pixPayment.UpdateProviderStatus(
                    mpOrder.Status,
                    mpOrder.StatusDetail,
                    mpOrder.TransactionStatus,
                    mpOrder.TransactionStatusDetail);
                webhookEvent.MarkIgnored($"Unhandled refund/chargeback status '{mpOrder.Status}'.");
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return new ProcessMercadoPagoPixWebhookResult(
                    200, "UnhandledStatus", $"Provider status '{mpOrder.Status}' ignored.", pixPayment.Id, pixPayment.OrderId);
            }

            logger.LogWarning(
                "Mercado Pago webhook ignored unknown status {Status}/{StatusDetail} for order {MaskedOrderId}.",
                mpOrder.Status,
                mpOrder.StatusDetail,
                MaskProviderOrderId(providerOrderId));

            pixPayment.UpdateProviderStatus(
                mpOrder.Status,
                mpOrder.StatusDetail,
                mpOrder.TransactionStatus,
                mpOrder.TransactionStatusDetail);
            webhookEvent.MarkIgnored($"Unhandled provider status '{mpOrder.Status}'.");
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return new ProcessMercadoPagoPixWebhookResult(
                200,
                "UnhandledStatus",
                $"Provider status '{mpOrder.Status}' was ignored.",
                pixPayment.Id,
                pixPayment.OrderId);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Mercado Pago webhook processing failed for order {MaskedOrderId}. FinalStatus=Failed",
                MaskProviderOrderId(providerOrderId));
            webhookEvent.MarkFailed(ex.Message);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Mercado Pago Orders API ids start with ORD (sandbox ORDTST…). Numeric panel-simulation ids are rejected.
    /// </summary>
    internal static bool IsValidMercadoPagoOrdersApiId(string providerOrderId)
    {
        var id = providerOrderId.Trim();
        return id.Length >= 4
               && id.StartsWith("ORD", StringComparison.OrdinalIgnoreCase);
    }

    internal static string MaskProviderOrderId(string providerOrderId)
    {
        var trimmed = providerOrderId.Trim();
        if (trimmed.Length <= 8)
            return "***";

        return $"{trimmed[..4]}…{trimmed[^4..]}";
    }

    private async Task<ProcessMercadoPagoPixWebhookResult> ProcessPaidAsync(
        PixPayment pixPayment,
        MercadoPagoOrderLookup mpOrder,
        MercadoPagoWebhookEvent webhookEvent,
        CancellationToken cancellationToken)
    {
        if (pixPayment.Status == PixPaymentStatus.Paid)
        {
            await TryConfirmReservationsForPaidOrderAsync(pixPayment.OrderId, cancellationToken);
            webhookEvent.MarkProcessed();
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return new ProcessMercadoPagoPixWebhookResult(
                200,
                "AlreadyPaid",
                "Pix payment was already Paid.",
                pixPayment.Id,
                pixPayment.OrderId);
        }

        if (pixPayment.Status != PixPaymentStatus.Pending)
        {
            logger.LogError(
                "Cannot mark PixPayment {PixPaymentId} as Paid from status {Status}.",
                pixPayment.Id,
                pixPayment.Status);

            webhookEvent.MarkFailed($"Local PixPayment status is {pixPayment.Status}.");
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return new ProcessMercadoPagoPixWebhookResult(
                200,
                "InvalidLocalStatus",
                $"Local Pix payment status is {pixPayment.Status}.",
                pixPayment.Id,
                pixPayment.OrderId);
        }

        var orderProbe = await orderPaidWriter.GetAsync(pixPayment.OrderId, cancellationToken);

        if (!orderProbe.Found)
        {
            webhookEvent.MarkFailed("Order not found.");
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return new ProcessMercadoPagoPixWebhookResult(
                200, "OrderNotFound", "Order was not found.", pixPayment.Id, pixPayment.OrderId);
        }

        if (!orderProbe.AlreadyPaid
            && !string.Equals(orderProbe.Status, "PendingPayment", StringComparison.Ordinal))
        {
            logger.LogError(
                "Mercado Pago order accredited but Shopflow Order {OrderId} status is {OrderStatus}; not marking Paid.",
                pixPayment.OrderId,
                orderProbe.Status);

            webhookEvent.MarkFailed($"Order status is {orderProbe.Status}.");
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return new ProcessMercadoPagoPixWebhookResult(
                200,
                "OrderNotPayable",
                $"Order status is {orderProbe.Status}; reservation was not confirmed.",
                pixPayment.Id,
                pixPayment.OrderId);
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

                    webhookEvent.MarkFailed($"Reservation confirm failed: {reservationId}");
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    return new ProcessMercadoPagoPixWebhookResult(
                        200,
                        "ReservationConfirmFailed",
                        "Could not confirm inventory reservation for approved payment.",
                        pixPayment.Id,
                        pixPayment.OrderId);
                }
            }
        }

        var approvedAt = mpOrder.LastUpdatedDate ?? DateTimeOffset.UtcNow;

        // Order and PixPayment live in separate DbContexts. Persist Order.Paid first so a
        // failed order write never leaves PixPayment=Paid while the order stays PendingPayment.
        // If payment SaveChanges fails afterward, retry sees Order already Paid and completes payment.
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

            webhookEvent.MarkFailed($"Order mark paid failed; status={orderWrite.Status}.");
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return new ProcessMercadoPagoPixWebhookResult(
                200,
                "OrderMarkPaidFailed",
                "Order could not be marked Paid; Pix payment was left Pending.",
                pixPayment.Id,
                pixPayment.OrderId);
        }

        pixPayment.MarkAsPaid(
            mpOrder.Status,
            mpOrder.StatusDetail,
            mpOrder.TransactionStatus,
            mpOrder.TransactionStatusDetail,
            approvedAt,
            mpOrder.Id,
            mpOrder.TransactionId);

        webhookEvent.MarkProcessed();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ProcessMercadoPagoPixWebhookResult(
            200,
            "Paid",
            "Pix payment and order marked Paid; reservations confirmed.",
            pixPayment.Id,
            pixPayment.OrderId);
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

    private async Task<PixPayment?> ResolvePixPaymentAsync(
        MercadoPagoOrderLookup mpOrder,
        string providerOrderId,
        CancellationToken cancellationToken)
    {
        var byOrderId = await paymentRepository.GetByProviderOrderIdAsync(providerOrderId, cancellationToken)
                        ?? (string.Equals(mpOrder.Id, providerOrderId, StringComparison.OrdinalIgnoreCase)
                            ? null
                            : await paymentRepository.GetByProviderOrderIdAsync(mpOrder.Id, cancellationToken));
        if (byOrderId is not null)
            return byOrderId;

        if (!string.IsNullOrWhiteSpace(mpOrder.TransactionId))
        {
            var byTx = await paymentRepository.GetByProviderPaymentIdAsync(mpOrder.TransactionId, cancellationToken);
            if (byTx is not null)
                return byTx;
        }

        if (Guid.TryParse(mpOrder.ExternalReference, out var orderId))
            return await paymentRepository.GetLatestByOrderIdAsync(orderId, cancellationToken);

        return null;
    }

    private static bool IsPixPaymentMethod(MercadoPagoOrderLookup order)
        => string.Equals(order.PaymentMethodId, "pix", StringComparison.OrdinalIgnoreCase)
           && (string.IsNullOrWhiteSpace(order.PaymentMethodType)
               || string.Equals(order.PaymentMethodType, "bank_transfer", StringComparison.OrdinalIgnoreCase));

    private static bool IsPaid(MercadoPagoOrderLookup order)
    {
        var statusOk = string.Equals(order.Status, "processed", StringComparison.OrdinalIgnoreCase);
        var detailOk = string.Equals(order.StatusDetail, "accredited", StringComparison.OrdinalIgnoreCase);
        if (!statusOk || !detailOk)
            return false;

        if (!string.IsNullOrWhiteSpace(order.TransactionStatus)
            && !string.Equals(order.TransactionStatus, "processed", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrWhiteSpace(order.TransactionStatusDetail)
            && !string.Equals(order.TransactionStatusDetail, "accredited", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    private static bool IsPending(MercadoPagoOrderLookup order)
    {
        if (!PendingStatuses.Contains(order.Status ?? string.Empty))
            return false;

        if (string.IsNullOrWhiteSpace(order.StatusDetail))
            return true;

        return PendingStatusDetails.Contains(order.StatusDetail);
    }

    private static bool AmountsMatch(decimal localAmount, decimal providerAmount)
        => Math.Abs(localAmount - providerAmount) < 0.01m;

    private static bool ExternalReferenceMatches(Guid orderId, string? externalReference)
    {
        if (string.IsNullOrWhiteSpace(externalReference))
            return false;

        return Guid.TryParse(externalReference, out var parsed) && parsed == orderId;
    }

    private void LogSignatureFailure(
        MercadoPagoWebhookSignatureValidationResult signatureResult,
        string? dataIdFromBody)
    {
        var d = signatureResult.Diagnostics;
        logger.LogWarning(
            "Mercado Pago webhook signature invalid. " +
            "has_x_signature={HasXSignature} has_x_request_id={HasXRequestId} has_query_data_id={HasQueryDataId} " +
            "has_body_data_id={HasBodyDataId} query_data_id_masked={QueryDataIdMasked} body_data_id_masked={BodyDataIdMasked} " +
            "data_id_query_was_lowercased={DataIdLowercased} ts_present={TsPresent} v1_present={V1Present} " +
            "request_id_masked={RequestIdMasked} secret_configured={SecretConfigured} " +
            "timestamp_age_seconds={TimestampAgeSeconds} timestamp_within_tolerance={TimestampWithinTolerance} " +
            "received_v1_prefix={ReceivedV1Prefix} computed_official_prefix={ComputedPrefix} " +
            "manifest_parts_included={ManifestParts} failure_reason={FailureReasonCode} detail={Detail}",
            d.HasXSignature,
            d.HasXRequestId,
            d.HasQueryDataId,
            !string.IsNullOrWhiteSpace(dataIdFromBody),
            d.QueryDataIdMasked,
            string.IsNullOrWhiteSpace(dataIdFromBody) ? null : MaskProviderOrderId(dataIdFromBody),
            d.DataIdQueryWasLowercased,
            d.TsPresent,
            d.V1Present,
            d.RequestIdMasked,
            d.SecretConfigured,
            d.TimestampAgeSeconds,
            d.TimestampWithinTolerance,
            d.ReceivedV1Prefix,
            d.ComputedOfficialPrefix,
            d.ManifestPartsIncluded,
            signatureResult.FailureReasonCode,
            signatureResult.FailureReason);
    }

    private async Task<(MercadoPagoWebhookEvent Event, ProcessMercadoPagoPixWebhookResult? AlreadyResult)>
        GetOrCreateWebhookEventAsync(
            string providerOrderId,
            ProcessMercadoPagoPixWebhookCommand command,
            CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(command.ProviderEventId))
        {
            var existing = await webhookEventRepository.GetByProviderEventIdAsync(
                command.ProviderEventId!,
                cancellationToken);

            if (existing is not null)
            {
                if (existing.ProcessingStatus is "Processed" or "Ignored" or "LookupFailed")
                {
                    var outcome = existing.ProcessingStatus switch
                    {
                        "Processed" => "AlreadyProcessed",
                        "LookupFailed" => "AlreadyLookupFailed",
                        _ => "AlreadyIgnored"
                    };
                    return (existing, new ProcessMercadoPagoPixWebhookResult(
                        200,
                        outcome,
                        $"Webhook event already {existing.ProcessingStatus.ToLowerInvariant()}."));
                }

                existing.ResetForReprocessing();
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return (existing, null);
            }
        }

        var webhookEvent = MercadoPagoWebhookEvent.CreateReceived(
            providerOrderId,
            command.ProviderEventId,
            command.XRequestId,
            command.Action,
            command.Type,
            command.LiveMode,
            signatureValid: true);
        await webhookEventRepository.AddAsync(webhookEvent, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return (webhookEvent, null);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
