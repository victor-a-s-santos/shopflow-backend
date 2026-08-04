namespace Vls.Shopflow.Notifications.Application.Interfaces;

public interface IEmailNotificationService
{
    Task EnqueueConfirmEmailAsync(
        string email,
        string fullName,
        string confirmationToken,
        CancellationToken cancellationToken = default);

    Task EnqueueResetPasswordAsync(
        string email,
        string? fullName,
        string resetToken,
        CancellationToken cancellationToken = default);

    Task EnqueueOrderCreatedAsync(
        OrderEmailNotificationRequest request,
        CancellationToken cancellationToken = default);

    Task EnqueuePaymentConfirmedAsync(
        OrderEmailNotificationRequest request,
        CancellationToken cancellationToken = default);

    Task EnqueueOrderShippedAsync(
        OrderEmailNotificationRequest request,
        CancellationToken cancellationToken = default);

    Task EnqueueOrderDeliveredAsync(
        OrderEmailNotificationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record OrderEmailNotificationRequest(
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
