namespace Vls.Shopflow.IdentityAccess.Application.Options;

public sealed class StoreAccessOptions
{
    public const string SectionName = "StoreAccess";

    /// <summary>
    /// Canonical: PublicCatalogAndGuestCheckout, PublicCatalogLoginCheckout,
    /// PublicCatalogApprovedCheckout, PrivateCatalogApprovedOnly.
    /// Aliases: Closed → PrivateCatalogApprovedOnly, Open → PublicCatalogAndGuestCheckout.
    /// Unknown values fail closed to PrivateCatalogApprovedOnly.
    /// </summary>
    public string Mode { get; set; } = "PrivateCatalogApprovedOnly";
}

public sealed class CheckoutAccessOptions
{
    public const string SectionName = "Checkout";

    public bool AllowGuestCheckout { get; set; }

    /// <summary>Alias of <see cref="AllowGuestCheckout"/> (rascunho Open/Closed).</summary>
    public bool AllowGuest { get; set; }

    public bool GuestCheckoutEnabled => AllowGuestCheckout || AllowGuest;
}

public sealed class CustomerAccessOptions
{
    public const string SectionName = "CustomerAccess";

    public bool RequireApproval { get; set; } = true;
}
