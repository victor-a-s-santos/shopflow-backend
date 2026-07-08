namespace Vls.Shopflow.Expiration.Application.Options;

public sealed class ExpirationWorkerOptions
{
    public const string SectionName = "ExpirationWorker";

    public bool Enabled { get; set; } = true;

    public int IntervalSeconds { get; set; } = 60;

    public int BatchSize { get; set; } = 50;

    public int CheckoutSessionTtlMinutes { get; set; } = 15;

    public int PixPaymentTtlMinutes { get; set; } = 15;
}
