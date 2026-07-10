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
