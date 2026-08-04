namespace Vls.Shopflow.Orders.Application.Interfaces;

/// <summary>
/// Port for transactional order emails. Null implementation by default; Notifications module registers the real one.
/// </summary>
public interface IOrderEmailNotifier
{
    Task NotifyOrderCreatedAsync(OrderEmailNotifyRequest request, CancellationToken cancellationToken = default);

    Task NotifyPaymentConfirmedAsync(OrderEmailNotifyRequest request, CancellationToken cancellationToken = default);

    Task NotifyOrderShippedAsync(OrderEmailNotifyRequest request, CancellationToken cancellationToken = default);

    Task NotifyOrderDeliveredAsync(OrderEmailNotifyRequest request, CancellationToken cancellationToken = default);
}

public sealed record OrderEmailNotifyRequest(
    Guid OrderId,
    long OrderNumber,
    string CustomerEmail,
    string CustomerName,
    decimal Total,
    Guid? CustomerUserId = null,
    string? GuestAccessToken = null,
    string? TrackingCode = null,
    string? FinalDeliveryMethod = null,
    string? PreferredDeliveryMethod = null,
    DateOnly? PreferredDeliveryDate = null);
