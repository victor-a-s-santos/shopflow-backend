namespace Vls.Shopflow.PaymentsPix.Application.Options;

public sealed class MercadoPagoOptions
{
    public const string SectionName = "MercadoPago";

    public bool Enabled { get; set; }

    public string Environment { get; set; } = "Sandbox";

    public string AccessToken { get; set; } = string.Empty;

    public string PublicKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.mercadopago.com";

    public string ApplicationId { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string WebhookSecret { get; set; } = string.Empty;

    public string NotificationUrl { get; set; } = string.Empty;

    public int PixExpirationMinutes { get; set; } = 30;

    public int WebhookSignatureToleranceMinutes { get; set; } = 10;

    /// <summary>
    /// TEMPORARY DIAGNOSTIC ONLY. When true (and ASPNETCORE_ENVIRONMENT != Production),
    /// logs a controlled raw webhook capture for SDK signature mismatch investigation.
    /// Must be removed / left false after diagnosis. Never enable in Production.
    /// </summary>
    public bool WebhookRawCaptureEnabled { get; set; }

    /// <summary>
    /// Optional ProviderOrderId (query data.id) filter for raw capture. Case-insensitive.
    /// </summary>
    public string? WebhookRawCaptureOrderId { get; set; }

    /// <summary>Max raw capture log events per process when OrderId filter is empty. Default 5.</summary>
    public int WebhookRawCaptureMaxEvents { get; set; } = 5;

    /// <summary>
    /// Sandbox-only override (e.g. "APRO") for predefined Pix test orders.
    /// </summary>
    public string? SandboxPayerFirstNameOverride { get; set; }

    /// <summary>Legacy alias for SandboxPayerFirstNameOverride.</summary>
    public string? TestPayerFirstName
    {
        get => SandboxPayerFirstNameOverride;
        set => SandboxPayerFirstNameOverride = value;
    }

    public string SandboxTestPayerEmail { get; set; } = "test_user_br@testuser.com";

    public bool IsSandbox
        => string.Equals(Environment, "Sandbox", StringComparison.OrdinalIgnoreCase);
}
