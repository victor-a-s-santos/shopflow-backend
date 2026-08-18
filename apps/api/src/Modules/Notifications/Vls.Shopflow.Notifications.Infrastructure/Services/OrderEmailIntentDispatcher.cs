using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vls.Shopflow.Notifications.Application.Interfaces;
using Vls.Shopflow.Notifications.Application.Options;
using Vls.Shopflow.Orders.Application.Models;
using Vls.Shopflow.Orders.Application.Repositories;
using Vls.Shopflow.Orders.Domain.Entities;
using Vls.Shopflow.Orders.Domain.Enums;

namespace Vls.Shopflow.Notifications.Infrastructure.Services;

public sealed class OrderEmailIntentDispatcher(
    IOrderEmailIntentRepository intents,
    IEmailNotificationService emails,
    IEmailOutboxRepository outbox,
    IOptions<OrderEmailIntentDispatcherOptions> options,
    ILogger<OrderEmailIntentDispatcher> logger) : IOrderEmailIntentDispatcher
{
    public async Task ProcessAsync(CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (!settings.Enabled)
            return;

        var batchSize = Math.Clamp(settings.BatchSize <= 0 ? 20 : settings.BatchSize, 1, 100);

        var repaired = await intents.RepairMissingIntentsAsync(batchSize, cancellationToken);
        if (repaired > 0)
        {
            await intents.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Repaired {RepairedCount} missing order email intents",
                repaired);
        }

        await intents.ExecutePendingBatchAsync(
            batchSize,
            (intent, ct) => DispatchOneAsync(intent, ct),
            cancellationToken);
    }

    private async Task<bool> DispatchOneAsync(OrderEmailIntent intent, CancellationToken cancellationToken)
    {
        try
        {
            if (await outbox.ExistsByIdempotencyKeyAsync(intent.IdempotencyKey, cancellationToken))
            {
                logger.LogInformation(
                    "Order email intent already in outbox IntentId={IntentId} OrderId={OrderId} EmailType={EmailType} IdempotencyKey={IdempotencyKey} Result={Result}",
                    intent.Id,
                    intent.OrderId,
                    intent.Type,
                    intent.IdempotencyKey,
                    "Dispatched");
                return true;
            }

            var payload = OrderEmailIntentPayloadJson.Deserialize(intent.PayloadJson);
            var request = ToRequest(intent.OrderId, payload);

            switch (intent.Type)
            {
                case OrderEmailIntentType.OrderCreated:
                    await emails.EnqueueOrderCreatedAsync(request, cancellationToken);
                    break;
                case OrderEmailIntentType.PaymentConfirmed:
                    await emails.EnqueuePaymentConfirmedAsync(request, cancellationToken);
                    break;
                case OrderEmailIntentType.OrderShipped:
                    await emails.EnqueueOrderShippedAsync(request, cancellationToken);
                    break;
                case OrderEmailIntentType.OrderDelivered:
                    await emails.EnqueueOrderDeliveredAsync(request, cancellationToken);
                    break;
                default:
                    logger.LogError(
                        "Unknown order email intent Type={EmailType} IntentId={IntentId} OrderId={OrderId}",
                        intent.Type,
                        intent.Id,
                        intent.OrderId);
                    return false;
            }

            var exists = await outbox.ExistsByIdempotencyKeyAsync(intent.IdempotencyKey, cancellationToken);
            if (!exists)
            {
                logger.LogWarning(
                    "Intent dispatch left outbox missing IntentId={IntentId} OrderId={OrderId} EmailType={EmailType} IdempotencyKey={IdempotencyKey}",
                    intent.Id,
                    intent.OrderId,
                    intent.Type,
                    intent.IdempotencyKey);
                return false;
            }

            logger.LogInformation(
                "Dispatched order email intent IntentId={IntentId} OrderId={OrderId} EmailType={EmailType} IdempotencyKey={IdempotencyKey} Result={Result}",
                intent.Id,
                intent.OrderId,
                intent.Type,
                intent.IdempotencyKey,
                "Dispatched");
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to dispatch order email intent IntentId={IntentId} OrderId={OrderId} EmailType={EmailType} IdempotencyKey={IdempotencyKey}",
                intent.Id,
                intent.OrderId,
                intent.Type,
                intent.IdempotencyKey);
            return false;
        }
    }

    private static OrderEmailNotificationRequest ToRequest(Guid orderId, OrderEmailIntentPayload payload)
        => new(
            orderId,
            payload.OrderNumber,
            payload.CustomerEmail,
            payload.CustomerName,
            payload.Total,
            payload.CustomerUserId,
            payload.GuestAccessToken,
            payload.TrackingCode,
            payload.FinalDeliveryMethod,
            payload.PreferredDeliveryMethod,
            payload.PreferredDeliveryDate);
}
