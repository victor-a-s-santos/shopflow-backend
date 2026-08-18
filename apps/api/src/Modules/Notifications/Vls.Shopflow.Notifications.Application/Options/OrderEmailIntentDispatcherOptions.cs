namespace Vls.Shopflow.Notifications.Application.Options;

public sealed class OrderEmailIntentDispatcherOptions
{
    public const string SectionName = "OrderEmailIntentDispatcher";

    public bool Enabled { get; set; } = true;
    public int IntervalSeconds { get; set; } = 15;
    public int BatchSize { get; set; } = 20;
}
