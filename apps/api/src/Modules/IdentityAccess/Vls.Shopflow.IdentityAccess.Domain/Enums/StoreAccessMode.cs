namespace Vls.Shopflow.IdentityAccess.Domain.Enums;

public enum StoreAccessMode
{
    PublicCatalogAndGuestCheckout = 0,
    PublicCatalogLoginCheckout = 1,
    PublicCatalogApprovedCheckout = 2,
    PrivateCatalogApprovedOnly = 3
}
