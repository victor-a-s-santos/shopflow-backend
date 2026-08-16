namespace Vls.Shopflow.Notifications.Application.Options;

public sealed class AdminNotificationsOptions
{
    public const string SectionName = "AdminNotifications";

    /// <summary>Operational inbox for pending customer approval requests. Empty = skip admin e-mail.</summary>
    public string ApprovalRequestsEmail { get; set; } = "";
}
