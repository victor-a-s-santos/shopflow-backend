namespace Vls.Shopflow.PaymentsPix.Application.Options;

public sealed class PaymentsPixOptions
{
    public const string SectionName = "PaymentsPix";

    /// <summary>
    /// Provider name: Fake (default) or MercadoPago.
    /// </summary>
    public string Provider { get; set; } = "Fake";
}
