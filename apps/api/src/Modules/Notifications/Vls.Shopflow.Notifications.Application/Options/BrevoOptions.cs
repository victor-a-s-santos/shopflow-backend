namespace Vls.Shopflow.Notifications.Application.Options;

public sealed class BrevoOptions
{
    public const string SectionName = "Brevo";

    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "https://api.brevo.com";
    public string ApiKey { get; set; } = "";
    public string SenderName { get; set; } = "Vip Assessoria";
    public string SenderEmail { get; set; } = "";
    public string? ReplyToEmail { get; set; }
    /// <summary>When true, still calls Brevo but tags sandbox; when provider is disabled or unconfigured, outbox stays Pending for retry.</summary>
    public bool SandboxMode { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 10;
}
