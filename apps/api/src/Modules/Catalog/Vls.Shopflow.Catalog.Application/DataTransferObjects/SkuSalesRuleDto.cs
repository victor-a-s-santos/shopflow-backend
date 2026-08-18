namespace Vls.Shopflow.Catalog.Application.DataTransferObjects;

/// <summary>
/// Admin/storefront sales rule for a SKU. Absent on write → Unit defaults.
/// </summary>
public sealed record SkuSalesRuleDto(
    string SalesMode,
    int MinimumQuantity,
    int QuantityStep,
    int? PackageSize,
    string? PackageLabel,
    string? PackageDescription,
    string? QuantityUnitLabel,
    bool AllowCustomerToChooseVariants,
    bool ShowTotalPieces,
    bool IsWholesaleOnly);

/// <summary>
/// Storefront-oriented labels and equivalent unit prices for lote/pacote SKUs.
/// Null when the SKU is not in a package mode. Avoids FE rounding divergence.
/// </summary>
public sealed record SkuSalesRuleDisplayDto(
    string SellingUnitLabel,
    int PackageSize,
    string PackageSizeLabel,
    string PackagePriceLabel,
    string EquivalentUnitPriceLabel,
    bool ShowEquivalentUnitPrice,
    decimal EquivalentRegularUnitPrice,
    decimal? EquivalentPromotionalUnitPrice);

/// <summary>
/// Optional write payload. Null/omitted fields are filled by mode defaults.
/// </summary>
public sealed record SkuSalesRuleWriteDto(
    string? SalesMode,
    int? MinimumQuantity,
    int? QuantityStep,
    int? PackageSize,
    string? PackageLabel,
    string? PackageDescription,
    string? QuantityUnitLabel,
    bool? AllowCustomerToChooseVariants,
    bool? ShowTotalPieces,
    bool? IsWholesaleOnly);
