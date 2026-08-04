using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vls.Shopflow.Notifications.Application.Interfaces;
using Vls.Shopflow.Notifications.Application.Models;
using Vls.Shopflow.Notifications.Application.Options;

namespace Vls.Shopflow.Notifications.Application.Services;

public sealed class EmailOutboxProcessor(
    IEmailOutboxRepository outbox,
    ITransactionalEmailSender sender,
    IOptions<EmailOutboxOptions> outboxOptions,
    IOptions<BrevoOptions> brevoOptions,
    ILogger<EmailOutboxProcessor> logger) : IEmailOutboxProcessor
{
    public async Task ProcessAsync(CancellationToken cancellationToken = default)
    {
        var settings = outboxOptions.Value;
        if (!settings.Enabled)
            return;

        var batchSize = Math.Clamp(settings.BatchSize <= 0 ? 20 : settings.BatchSize, 1, 100);
        var maxAttempts = Math.Max(1, settings.MaxAttempts);
        var messages = await outbox.ClaimPendingBatchAsync(batchSize, cancellationToken);

        if (messages.Count == 0)
            return;

        var brevoEnabled = brevoOptions.Value.Enabled
                           && !string.IsNullOrWhiteSpace(brevoOptions.Value.ApiKey)
                           && !string.IsNullOrWhiteSpace(brevoOptions.Value.SenderEmail);

        foreach (var message in messages)
        {
            if (!brevoEnabled)
            {
                message.MarkSkipped("Brevo disabled or missing ApiKey/SenderEmail.");
                logger.LogInformation(
                    "Email outbox skipped Type={Type} OutboxId={OutboxId} (Brevo disabled)",
                    message.Type,
                    message.Id);
                continue;
            }

            try
            {
                var result = await sender.SendAsync(
                    new TransactionalEmailMessage(
                        message.RecipientEmail,
                        message.RecipientName,
                        message.Subject,
                        message.HtmlBody,
                        message.TextBody),
                    cancellationToken);

                if (result.Succeeded)
                {
                    message.MarkSent(result.ProviderMessageId);
                    logger.LogInformation(
                        "Email outbox sent Type={Type} OutboxId={OutboxId} ProviderMessageId={ProviderMessageId}",
                        message.Type,
                        message.Id,
                        result.ProviderMessageId);
                    continue;
                }

                var error = result.ErrorMessage ?? "Unknown provider error";
                if (result.IsTransientFailure && message.Attempts + 1 < maxAttempts)
                {
                    var delay = TimeSpan.FromSeconds(Math.Min(3600, Math.Pow(2, message.Attempts + 1) * 15));
                    message.MarkRetry(error, DateTimeOffset.UtcNow.Add(delay));
                    logger.LogWarning(
                        "Email outbox retry scheduled Type={Type} OutboxId={OutboxId} Attempts={Attempts} NextAttemptAt={NextAttemptAt}",
                        message.Type,
                        message.Id,
                        message.Attempts,
                        message.NextAttemptAt);
                }
                else
                {
                    message.MarkFailed(error);
                    logger.LogError(
                        "Email outbox failed permanently Type={Type} OutboxId={OutboxId} Attempts={Attempts}",
                        message.Type,
                        message.Id,
                        message.Attempts);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (message.Attempts + 1 < maxAttempts)
                {
                    var delay = TimeSpan.FromSeconds(Math.Min(3600, Math.Pow(2, message.Attempts + 1) * 15));
                    message.MarkRetry(ex.Message, DateTimeOffset.UtcNow.Add(delay));
                    logger.LogWarning(
                        ex,
                        "Email outbox exception — retry Type={Type} OutboxId={OutboxId}",
                        message.Type,
                        message.Id);
                }
                else
                {
                    message.MarkFailed(ex.Message);
                    logger.LogError(
                        ex,
                        "Email outbox exception — failed Type={Type} OutboxId={OutboxId}",
                        message.Type,
                        message.Id);
                }
            }
        }

        await outbox.SaveChangesAsync(cancellationToken);
    }
}
