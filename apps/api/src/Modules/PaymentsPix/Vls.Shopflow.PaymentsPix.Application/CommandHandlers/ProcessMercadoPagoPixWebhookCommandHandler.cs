using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vls.Shopflow.PaymentsPix.Application.Commands;
using Vls.Shopflow.PaymentsPix.Application.Interfaces;
using Vls.Shopflow.PaymentsPix.Application.Options;
using Vls.Shopflow.PaymentsPix.Application.Repositories;
using Vls.Shopflow.PaymentsPix.Application.Security;
using Vls.Shopflow.PaymentsPix.Application.Services;
using Vls.Shopflow.PaymentsPix.Domain.Entities;
using Vls.Shopflow.PaymentsPix.Domain.Enums;

namespace Vls.Shopflow.PaymentsPix.Application.CommandHandlers;

public sealed class ProcessMercadoPagoPixWebhookCommandHandler(
    IOptions<MercadoPagoOptions> mercadoPagoOptions,
    IMercadoPagoWebhookSignatureValidator signatureValidator,
    IMercadoPagoWebhookRawCapture webhookRawCapture,
    IMercadoPagoOrderClient orderClient,
    IPixPaymentRepository paymentRepository,
    IMercadoPagoWebhookEventRepository webhookEventRepository,
    IMercadoPagoPixPaidTransitionService paidTransition,
    IPaymentsPixUnitOfWork unitOfWork,
    ILogger<ProcessMercadoPagoPixWebhookCommandHandler> logger)
    : IRequestHandler<ProcessMercadoPagoPixWebhookCommand, ProcessMercadoPagoPixWebhookResult>
{

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

        LogWebhookEnvelope(command, dataIdFromQuery, dataIdFromBody, signatureResult.IsValid, signatureResult.FailureReasonCode);

        // TEMPORARY DIAGNOSTIC ONLY — gated inside capture service (never Production).
        webhookRawCapture.TryCapture(
            new MercadoPagoWebhookRawCaptureInput(
                DateTimeOffset.UtcNow,
                command.RequestMethod ?? "POST",
                command.RequestPath ?? "/api/payments/pix/webhooks/mercado-pago",
                command.RawQueryString,
                command.DataIdFromQuery,
                command.QueryTypeExact,
                command.XRequestId,
                command.XSignature,
                command.BodyRawJson,
                command.ApplicationId,
                command.UserId,
                command.LiveMode,
                command.Type,
                command.Action,
                command.DataIdFromBody,
                command.DataStatus,
                command.DataStatusDetail),
            signatureResult);

        if (!signatureResult.IsValid)
        {
            LogSignatureFailure(signatureResult, command, dataIdFromQuery, dataIdFromBody, options);
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
            "Mercado Pago webhook accepted after signature. MaskedOrderId={MaskedOrderId} Type={Type} Action={Action}",
            MaskProviderOrderId(providerOrderId),
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

            if (!MercadoPagoOrderStatusRules.IsPixPaymentMethod(mpOrder))
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

            if (!MercadoPagoOrderStatusRules.AmountsMatch(pixPayment.Amount, mpOrder.TotalAmount)
                || (mpOrder.TransactionAmount is { } txAmount
                    && !MercadoPagoOrderStatusRules.AmountsMatch(pixPayment.Amount, txAmount)))
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

            if (!MercadoPagoOrderStatusRules.ExternalReferenceMatches(pixPayment.OrderId, mpOrder.ExternalReference))
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

            if (MercadoPagoOrderStatusRules.IsPaid(mpOrder))
            {
                return await ProcessPaidAsync(pixPayment, mpOrder, webhookEvent, cancellationToken);
            }

            if (MercadoPagoOrderStatusRules.IsPending(mpOrder))
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
        var transition = await paidTransition.ApplyPaidAsync(pixPayment, mpOrder, cancellationToken);

        if (transition.Success)
            webhookEvent.MarkProcessed();
        else
            webhookEvent.MarkFailed(transition.Message);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ProcessMercadoPagoPixWebhookResult(
            200,
            transition.Outcome,
            transition.Message,
            pixPayment.Id,
            pixPayment.OrderId);
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

    private void LogWebhookEnvelope(
        ProcessMercadoPagoPixWebhookCommand command,
        string? dataIdFromQuery,
        string? dataIdFromBody,
        bool signatureValid,
        string? failureReasonCode)
    {
        var options = mercadoPagoOptions.Value;
        var bodyAppId = Normalize(command.ApplicationId);
        var bodyUserId = Normalize(command.UserId);
        var configuredAppId = Normalize(options.ApplicationId);
        var configuredUserId = Normalize(options.UserId);

        // Signature diagnostics (sdk/manual) are logged in detail on mismatch; keep envelope lean on success.
        logger.LogInformation(
            "Mercado Pago webhook envelope. " +
            "body_application_id={BodyApplicationId} configured_application_id={ConfiguredApplicationId} " +
            "application_id_matches_config={ApplicationIdMatches} " +
            "body_user_id={BodyUserId} configured_user_id={ConfiguredUserId} user_id_matches_config={UserIdMatches} " +
            "body_live_mode={BodyLiveMode} configured_environment={ConfiguredEnvironment} " +
            "type={Type} action={Action} " +
            "query_data_id_masked={QueryDataIdMasked} body_data_id_masked={BodyDataIdMasked} " +
            "data_status={DataStatus} data_status_detail={DataStatusDetail} " +
            "signature_valid={SignatureValid} failure_reason={FailureReason}",
            bodyAppId,
            configuredAppId,
            IdsMatch(bodyAppId, configuredAppId),
            bodyUserId,
            configuredUserId,
            IdsMatch(bodyUserId, configuredUserId),
            command.LiveMode,
            options.Environment,
            command.Type,
            command.Action,
            string.IsNullOrWhiteSpace(dataIdFromQuery) ? null : MaskProviderOrderId(dataIdFromQuery),
            string.IsNullOrWhiteSpace(dataIdFromBody) ? null : MaskProviderOrderId(dataIdFromBody),
            command.DataStatus,
            command.DataStatusDetail,
            signatureValid,
            signatureValid ? null : failureReasonCode);
    }

    private void LogSignatureFailure(
        MercadoPagoWebhookSignatureValidationResult signatureResult,
        ProcessMercadoPagoPixWebhookCommand command,
        string? dataIdFromQuery,
        string? dataIdFromBody,
        MercadoPagoOptions options)
    {
        var d = signatureResult.Diagnostics;
        var secretFingerprint = MercadoPagoSecretFingerprint.Compute(options.WebhookSecret);
        var bodyAppId = Normalize(command.ApplicationId);
        var bodyUserId = Normalize(command.UserId);
        var configuredAppId = Normalize(options.ApplicationId);
        var configuredUserId = Normalize(options.UserId);

        logger.LogWarning(
            "Mercado Pago webhook signature invalid. " +
            "sdk_signature_valid={SdkValid} manual_signature_valid={ManualValid} " +
            "signature_validator_final={ValidatorFinal} sdk_exception_type={SdkExceptionType} " +
            "manual_failure_reason={ManualFailure} " +
            "has_x_signature={HasXSignature} has_x_request_id={HasXRequestId} has_query_data_id={HasQueryDataId} " +
            "has_body_data_id={HasBodyDataId} query_data_id_masked={QueryDataIdMasked} body_data_id_masked={BodyDataIdMasked} " +
            "data_id_query_was_lowercased={DataIdLowercased} ts_present={TsPresent} v1_present={V1Present} " +
            "request_id_masked={RequestIdMasked} secret_configured={SecretConfigured} " +
            "webhook_secret_fingerprint={WebhookSecretFingerprint} secret_length={SecretLength} " +
            "secret_trimmed_changed={SecretTrimmedChanged} " +
            "timestamp_age_seconds={TimestampAgeSeconds} timestamp_within_tolerance={TimestampWithinTolerance} " +
            "received_v1_prefix={ReceivedV1Prefix} computed_official_prefix={ComputedPrefix} " +
            "manifest_parts_included={ManifestParts} failure_reason={FailureReasonCode} detail={Detail} " +
            "body_application_id={BodyApplicationId} configured_application_id={ConfiguredApplicationId} " +
            "application_id_matches_config={ApplicationIdMatches} " +
            "body_user_id={BodyUserId} configured_user_id={ConfiguredUserId} user_id_matches_config={UserIdMatches} " +
            "body_live_mode={BodyLiveMode} configured_environment={ConfiguredEnvironment} type={Type} action={Action}",
            d.SdkSignatureValid,
            d.ManualSignatureValid,
            d.SignatureValidatorFinal,
            d.SdkExceptionType,
            d.ManualFailureReason,
            d.HasXSignature,
            d.HasXRequestId,
            d.HasQueryDataId,
            !string.IsNullOrWhiteSpace(dataIdFromBody),
            d.QueryDataIdMasked ?? (string.IsNullOrWhiteSpace(dataIdFromQuery) ? null : MaskProviderOrderId(dataIdFromQuery)),
            string.IsNullOrWhiteSpace(dataIdFromBody) ? null : MaskProviderOrderId(dataIdFromBody),
            d.DataIdQueryWasLowercased,
            d.TsPresent,
            d.V1Present,
            d.RequestIdMasked,
            d.SecretConfigured,
            d.WebhookSecretFingerprint ?? secretFingerprint,
            d.SecretLength,
            d.SecretTrimmedChanged,
            d.TimestampAgeSeconds,
            d.TimestampWithinTolerance,
            d.ReceivedV1Prefix,
            d.ComputedOfficialPrefix,
            d.ManifestPartsIncluded,
            signatureResult.FailureReasonCode,
            signatureResult.FailureReason,
            bodyAppId,
            configuredAppId,
            IdsMatch(bodyAppId, configuredAppId),
            bodyUserId,
            configuredUserId,
            IdsMatch(bodyUserId, configuredUserId),
            command.LiveMode,
            options.Environment,
            command.Type,
            command.Action);
    }

    private static bool? IdsMatch(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return null;

        return string.Equals(left, right, StringComparison.Ordinal);
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
