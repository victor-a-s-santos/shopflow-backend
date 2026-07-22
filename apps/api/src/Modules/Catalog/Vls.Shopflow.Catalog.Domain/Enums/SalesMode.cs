namespace Vls.Shopflow.Catalog.Domain.Enums;

/// <summary>
/// How a SKU can be purchased. ClosedGrid is intentionally omitted (pós-MVP).
/// </summary>
public enum SalesMode
{
    Unit = 0,
    MinimumQuantity = 1,
    MultipleQuantity = 2,
    FixedPackage = 3,
    AssortedPackage = 4
}
