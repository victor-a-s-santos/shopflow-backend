namespace Vls.Shopflow.Orders.Application.Options;

public sealed class FulfillmentOptions
{
    public const string SectionName = "Fulfillment";

    /// <summary>
    /// When true, admin must confirm physical supplier stock before marking a paid
    /// order (or remessa) as separated/shipped. Default false preserves the
    /// traditional AwaitingShipment → Shipped flow for future stores.
    /// </summary>
    public bool RequireStockConfirmation { get; set; }
}
