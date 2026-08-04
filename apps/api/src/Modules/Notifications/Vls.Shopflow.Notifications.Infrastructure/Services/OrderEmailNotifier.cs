using Microsoft.Extensions.Logging;
using Vls.Shopflow.Notifications.Application.Interfaces;
using Vls.Shopflow.Orders.Application.Interfaces;

namespace Vls.Shopflow.Notifications.Infrastructure.Services;

public sealed class OrderEmailNotifier(
    IEmailNotificationService notifications,
    ILogger<OrderEmailNotifier> logger) : IOrderEmailNotifier
{
    public Task NotifyOrderCreatedAsync(OrderEmailNotifyRequest request, CancellationToken cancellationToken = default)
        => SafeEnqueue(() => notifications.EnqueueOrderCreatedAsync(Map(request), cancellationToken), "OrderCreated", request.OrderId);

    public Task NotifyPaymentConfirmedAsync(OrderEmailNotifyRequest request, CancellationToken cancellationToken = default)
        => SafeEnqueue(() => notifications.EnqueuePaymentConfirmedAsync(Map(request), cancellationToken), "PaymentConfirmed", request.OrderId);

    public Task NotifyOrderShippedAsync(OrderEmailNotifyRequest request, CancellationToken cancellationToken = default)
        => SafeEnqueue(() => notifications.EnqueueOrderShippedAsync(Map(request), cancellationToken), "OrderShipped", request.OrderId);

    public Task NotifyOrderDeliveredAsync(OrderEmailNotifyRequest request, CancellationToken cancellationToken = default)
        => SafeEnqueue(() => notifications.EnqueueOrderDeliveredAsync(Map(request), cancellationToken), "OrderDelivered", request.OrderId);

    private async Task SafeEnqueue(Func<Task> action, string kind, Guid orderId)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to enqueue {Kind} email for order {OrderId}", kind, orderId);
        }
    }

    private static OrderEmailNotificationRequest Map(OrderEmailNotifyRequest r)
        => new(
            r.OrderId,
            r.OrderNumber,
            r.CustomerEmail,
            r.CustomerName,
            r.Total,
            r.CustomerUserId,
            r.GuestAccessToken,
            r.TrackingCode,
            r.FinalDeliveryMethod,
            r.PreferredDeliveryMethod,
            r.PreferredDeliveryDate);
}
