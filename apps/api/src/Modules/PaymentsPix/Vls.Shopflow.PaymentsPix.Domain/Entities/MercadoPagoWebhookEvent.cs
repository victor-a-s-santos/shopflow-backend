namespace Vls.Shopflow.PaymentsPix.Domain.Entities;

public sealed class MercadoPagoWebhookEvent
{
    public Guid Id { get; private set; }
    public string? ProviderEventId { get; private set; }
    public string ProviderOrderId { get; private set; } = default!;
    public string? RequestId { get; private set; }
    public string? Action { get; private set; }
    public string? Type { get; private set; }
    public bool LiveMode { get; private set; }
    public bool SignatureValid { get; private set; }
    public string ProcessingStatus { get; private set; } = default!;
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }

    private MercadoPagoWebhookEvent() { }

    public static MercadoPagoWebhookEvent CreateReceived(
        string providerOrderId,
        string? providerEventId,
        string? requestId,
        string? action,
        string? type,
        bool liveMode,
        bool signatureValid)
    {
        return new MercadoPagoWebhookEvent
        {
            Id = Guid.NewGuid(),
            ProviderOrderId = providerOrderId.Trim(),
            ProviderEventId = string.IsNullOrWhiteSpace(providerEventId) ? null : providerEventId.Trim(),
            RequestId = string.IsNullOrWhiteSpace(requestId) ? null : requestId.Trim(),
            Action = string.IsNullOrWhiteSpace(action) ? null : Truncate(action, 100),
            Type = string.IsNullOrWhiteSpace(type) ? null : Truncate(type, 50),
            LiveMode = liveMode,
            SignatureValid = signatureValid,
            ProcessingStatus = "Received",
            ReceivedAt = DateTimeOffset.UtcNow
        };
    }

    public void MarkProcessed()
    {
        ProcessingStatus = "Processed";
        ProcessedAt = DateTimeOffset.UtcNow;
        ErrorMessage = null;
    }

    public void MarkIgnored(string reason)
    {
        ProcessingStatus = "Ignored";
        ProcessedAt = DateTimeOffset.UtcNow;
        ErrorMessage = Truncate(reason, 500);
    }

    public void MarkFailed(string reason)
    {
        ProcessingStatus = "Failed";
        ProcessedAt = DateTimeOffset.UtcNow;
        ErrorMessage = Truncate(reason, 500);
    }

    /// <summary>
    /// Resets a Received/Failed row so the same ProviderEventId can be retried
    /// without inserting a duplicate (unique index).
    /// </summary>
    public void ResetForReprocessing()
    {
        ProcessingStatus = "Received";
        ErrorMessage = null;
        ProcessedAt = null;
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}
