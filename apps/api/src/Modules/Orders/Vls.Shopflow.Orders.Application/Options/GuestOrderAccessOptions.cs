namespace Vls.Shopflow.Orders.Application.Options;

public sealed class GuestOrderAccessOptions
{
    public const string SectionName = "GuestOrderAccess";

    public bool Enabled { get; set; } = false;

    public int TokenTtlDays { get; set; } = 30;

    public string TokenHashSecret { get; set; } = string.Empty;

    public int RateLimitPerMinute { get; set; } = 30;
}
