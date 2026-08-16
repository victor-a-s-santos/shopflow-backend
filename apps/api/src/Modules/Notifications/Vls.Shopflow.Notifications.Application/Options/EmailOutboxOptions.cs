namespace Vls.Shopflow.Notifications.Application.Options;

public sealed class EmailOutboxOptions
{
    public const string SectionName = "EmailOutbox";

    public bool Enabled { get; set; } = true;
    public int IntervalSeconds { get; set; } = 15;
    public int BatchSize { get; set; } = 20;
    public int MaxAttempts { get; set; } = 8;
    public int ProcessingTimeoutSeconds { get; set; } = 120;
}
