namespace Vls.Shopflow.PaymentsPix.Application.Options;

/// <summary>
/// Fallback polling of pending Mercado Pago Pix via GET /v1/orders/{ProviderOrderId}.
/// Does not replace webhooks.
/// </summary>
public sealed class MercadoPagoReconciliationOptions
{
    public const string SectionName = "MercadoPagoReconciliation";

    public bool Enabled { get; set; }

    public int IntervalSeconds { get; set; } = 60;

    public int BatchSize { get; set; } = 20;

    /// <summary>Only reconcile Pending MercadoPago payments created within this window.</summary>
    public int MaxAgeMinutes { get; set; } = 180;
}
