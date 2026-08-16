using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vls.Shopflow.Notifications.Application.Interfaces;
using Vls.Shopflow.Notifications.Application.Options;
using Vls.Shopflow.Notifications.Application.Templates;
using Vls.Shopflow.Notifications.Domain.Entities;
using Vls.Shopflow.Notifications.Domain.Enums;

namespace Vls.Shopflow.Notifications.Application.Services;

public sealed class EmailNotificationService(
    IEmailOutboxRepository outbox,
    IOptions<PublicAppOptions> publicAppOptions,
    ILogger<EmailNotificationService> logger) : IEmailNotificationService
{
    public Task EnqueueConfirmEmailAsync(
        string email,
        string fullName,
        string confirmationToken,
        CancellationToken cancellationToken = default)
    {
        var app = publicAppOptions.Value;
        var (subject, html, text) = TransactionalEmailTemplates.ConfirmEmail(
            app, email, fullName, confirmationToken);
        var key = $"customer:confirm-email:{HashToken(email, confirmationToken)}";
        return EnqueueAsync(
            EmailNotificationType.ConfirmEmail,
            email,
            fullName,
            subject,
            html,
            text,
            key,
            cancellationToken);
    }

    public Task EnqueueResetPasswordAsync(
        string email,
        string? fullName,
        string resetToken,
        CancellationToken cancellationToken = default)
    {
        var app = publicAppOptions.Value;
        var (subject, html, text) = TransactionalEmailTemplates.ResetPassword(
            app, email, fullName, resetToken);
        var key = $"customer:reset-password:{HashToken(email, resetToken)}";
        return EnqueueAsync(
            EmailNotificationType.ResetPassword,
            email,
            fullName,
            subject,
            html,
            text,
            key,
            cancellationToken);
    }

    public Task EnqueueOrderCreatedAsync(
        OrderEmailNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var (subject, html, text) = TransactionalEmailTemplates.OrderCreated(publicAppOptions.Value, request);
        return EnqueueAsync(
            EmailNotificationType.OrderCreated,
            request.CustomerEmail,
            request.CustomerName,
            subject,
            html,
            text,
            $"order:{request.OrderId:D}:created",
            cancellationToken);
    }

    public Task EnqueuePaymentConfirmedAsync(
        OrderEmailNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var (subject, html, text) = TransactionalEmailTemplates.PaymentConfirmed(publicAppOptions.Value, request);
        return EnqueueAsync(
            EmailNotificationType.PaymentConfirmed,
            request.CustomerEmail,
            request.CustomerName,
            subject,
            html,
            text,
            $"order:{request.OrderId:D}:paid",
            cancellationToken);
    }

    public Task EnqueueOrderShippedAsync(
        OrderEmailNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var (subject, html, text) = TransactionalEmailTemplates.OrderShipped(publicAppOptions.Value, request);
        return EnqueueAsync(
            EmailNotificationType.OrderShipped,
            request.CustomerEmail,
            request.CustomerName,
            subject,
            html,
            text,
            $"order:{request.OrderId:D}:shipped",
            cancellationToken);
    }

    public Task EnqueueOrderDeliveredAsync(
        OrderEmailNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var (subject, html, text) = TransactionalEmailTemplates.OrderDelivered(publicAppOptions.Value, request);
        return EnqueueAsync(
            EmailNotificationType.OrderDelivered,
            request.CustomerEmail,
            request.CustomerName,
            subject,
            html,
            text,
            $"order:{request.OrderId:D}:delivered",
            cancellationToken);
    }

    private async Task EnqueueAsync(
        EmailNotificationType type,
        string email,
        string? name,
        string subject,
        string html,
        string text,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (await outbox.ExistsByIdempotencyKeyAsync(idempotencyKey, cancellationToken))
        {
            logger.LogInformation(
                "Email outbox skip duplicate Type={Type} IdempotencyKey={IdempotencyKey}",
                type,
                idempotencyKey);
            return;
        }

        var message = EmailOutboxMessage.Create(
            type,
            email,
            name,
            subject,
            html,
            text,
            idempotencyKey);

        var inserted = await outbox.TryAddNewAsync(message, cancellationToken);
        if (!inserted)
        {
            logger.LogInformation(
                "Email outbox idempotent insert Type={Type} IdempotencyKey={IdempotencyKey}",
                type,
                idempotencyKey);
            return;
        }

        logger.LogInformation(
            "Email outbox enqueued Type={Type} OutboxId={OutboxId} IdempotencyKey={IdempotencyKey}",
            type,
            message.Id,
            idempotencyKey);
    }

    /// <summary>Short hash for idempotency — never store/log raw tokens.</summary>
    internal static string HashToken(string email, string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{email.Trim().ToLowerInvariant()}|{token}"));
        return Convert.ToHexString(bytes.AsSpan(0, 16)).ToLowerInvariant();
    }
}
