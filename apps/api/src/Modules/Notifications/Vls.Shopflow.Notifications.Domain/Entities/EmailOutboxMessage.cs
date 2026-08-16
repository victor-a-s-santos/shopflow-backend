using Vls.Shopflow.BuildingBlocks.Domain.Entities;
using Vls.Shopflow.Notifications.Domain.Enums;

namespace Vls.Shopflow.Notifications.Domain.Entities;

public sealed class EmailOutboxMessage : Entity<Guid>
{
    public const int MaxRecipientEmailLength = 320;
    public const int MaxRecipientNameLength = 200;
    public const int MaxSubjectLength = 300;
    public const int MaxIdempotencyKeyLength = 200;
    public const int MaxLastErrorLength = 2000;
    public const int MaxProviderMessageIdLength = 200;

    public EmailNotificationType Type { get; private set; }
    public string RecipientEmail { get; private set; } = default!;
    public string? RecipientName { get; private set; }
    public string Subject { get; private set; } = default!;
    public string HtmlBody { get; private set; } = default!;
    public string? TextBody { get; private set; }
    public string? PayloadJson { get; private set; }
    public EmailOutboxStatus Status { get; private set; }
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }
    public string? ProviderMessageId { get; private set; }
    public string IdempotencyKey { get; private set; } = default!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }
    public DateTimeOffset NextAttemptAt { get; private set; }
    public DateTimeOffset? ProcessingStartedAt { get; private set; }

    private EmailOutboxMessage() { }

    public static EmailOutboxMessage Create(
        EmailNotificationType type,
        string recipientEmail,
        string? recipientName,
        string subject,
        string htmlBody,
        string? textBody,
        string idempotencyKey,
        string? payloadJson = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(htmlBody);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        var now = DateTimeOffset.UtcNow;
        return new EmailOutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = type,
            RecipientEmail = recipientEmail.Trim(),
            RecipientName = string.IsNullOrWhiteSpace(recipientName) ? null : recipientName.Trim(),
            Subject = subject.Trim(),
            HtmlBody = htmlBody,
            TextBody = string.IsNullOrWhiteSpace(textBody) ? null : textBody,
            PayloadJson = payloadJson,
            Status = EmailOutboxStatus.Pending,
            Attempts = 0,
            IdempotencyKey = idempotencyKey.Trim(),
            CreatedAt = now,
            NextAttemptAt = now
        };
    }

    public void MarkProcessing()
    {
        Status = EmailOutboxStatus.Processing;
        ProcessingStartedAt = DateTimeOffset.UtcNow;
    }

    public void MarkSent(string? providerMessageId)
    {
        Status = EmailOutboxStatus.Sent;
        SentAt = DateTimeOffset.UtcNow;
        ProcessingStartedAt = null;
        ProviderMessageId = Truncate(providerMessageId, MaxProviderMessageIdLength);
        LastError = null;
    }

    public void MarkSkipped(string reason)
    {
        Status = EmailOutboxStatus.Skipped;
        SentAt = DateTimeOffset.UtcNow;
        ProcessingStartedAt = null;
        LastError = Truncate(reason, MaxLastErrorLength);
    }

    public void MarkRetry(string error, DateTimeOffset nextAttemptAt)
    {
        Attempts += 1;
        Status = EmailOutboxStatus.Pending;
        ProcessingStartedAt = null;
        LastError = Truncate(error, MaxLastErrorLength);
        NextAttemptAt = nextAttemptAt;
    }

    public void MarkFailed(string error)
    {
        Attempts += 1;
        Status = EmailOutboxStatus.Failed;
        ProcessingStartedAt = null;
        LastError = Truncate(error, MaxLastErrorLength);
    }

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }
}
