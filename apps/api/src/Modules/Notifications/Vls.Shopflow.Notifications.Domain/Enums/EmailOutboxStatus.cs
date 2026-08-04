namespace Vls.Shopflow.Notifications.Domain.Enums;

public enum EmailOutboxStatus
{
    Pending = 0,
    Processing = 1,
    Sent = 2,
    Failed = 3,
    Skipped = 4
}
