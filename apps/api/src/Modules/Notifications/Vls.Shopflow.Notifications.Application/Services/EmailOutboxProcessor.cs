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
        var processingTimeout = TimeSpan.FromSeconds(
            settings.ProcessingTimeoutSeconds <= 0 ? 120 : settings.ProcessingTimeoutSeconds);
        var messages = await outbox.ClaimPendingBatchAsync(batchSize, processingTimeout, cancellationToken);

        if (messages.Count == 0)
            return;

        var brevo = brevoOptions.Value;
        var brevoEnabled = brevo.Enabled
                           && !string.IsNullOrWhiteSpace(brevo.ApiKey)
                           && !string.IsNullOrWhiteSpace(brevo.SenderEmail);
        var configurationRetryDelay = TimeSpan.FromSeconds(
            settings.IntervalSeconds <= 0 ? 15 : settings.IntervalSeconds);

        foreach (var message in messages)
        {
            if (!brevoEnabled)
            {
                var configError = ResolveBrevoConfigurationError(brevo);
                message.ReleaseForConfigurationRetry(
                    configError,
                    DateTimeOffset.UtcNow.Add(configurationRetryDelay));
                logger.LogWarning(
                    "Email outbox waiting for Brevo configuration Type={Type} OutboxId={OutboxId} NextAttemptAt={NextAttemptAt}",
                    message.Type,
                    message.Id,
                    message.NextAttemptAt);
                continue;
            }

            try
            {
                var started = DateTimeOffset.UtcNow;
                var result = await sender.SendAsync(
                    new TransactionalEmailMessage(
                        message.RecipientEmail,
                        message.RecipientName,
                        message.Subject,
                        message.HtmlBody,
                        message.TextBody),
                    cancellationToken);
                var durationMs = (DateTimeOffset.UtcNow - started).TotalMilliseconds;

                if (result.Succeeded)
                {
                    message.MarkSent(result.ProviderMessageId);
                    logger.LogInformation(
                        "Email outbox sent Type={Type} OutboxId={OutboxId} IdempotencyKey={IdempotencyKey} Provider={Provider} ProviderMessageId={ProviderMessageId} Attempt={Attempt} DurationMs={DurationMs} Result={Result}",
                        message.Type,
                        message.Id,
                        message.IdempotencyKey,
                        "Brevo",
                        result.ProviderMessageId,
                        message.Attempts,
                        durationMs,
                        "Sent");
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

    private static string ResolveBrevoConfigurationError(BrevoOptions brevo)
    {
        if (!brevo.Enabled)
            return "Brevo disabled (Brevo__Enabled=false).";
        if (string.IsNullOrWhiteSpace(brevo.ApiKey))
            return "Brevo ApiKey is not configured.";
        return "Brevo SenderEmail is not configured.";
    }
}
