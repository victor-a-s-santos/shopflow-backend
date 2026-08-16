namespace Vls.Shopflow.IdentityAccess.Application.Options;

public sealed class StoreAccessOptions
{
    public const string SectionName = "StoreAccess";

    /// <summary>
    /// One of PublicCatalogAndGuestCheckout, PublicCatalogLoginCheckout,
    /// PublicCatalogApprovedCheckout, PrivateCatalogApprovedOnly.
    /// Unknown values fail closed to PrivateCatalogApprovedOnly.
    /// </summary>
    public string Mode { get; set; } = "PrivateCatalogApprovedOnly";
}

public sealed class CheckoutAccessOptions
{
    public const string SectionName = "Checkout";

    public bool AllowGuestCheckout { get; set; }
}

public sealed class CustomerAccessOptions
{
    public const string SectionName = "CustomerAccess";

    public bool RequireApproval { get; set; } = true;
}
