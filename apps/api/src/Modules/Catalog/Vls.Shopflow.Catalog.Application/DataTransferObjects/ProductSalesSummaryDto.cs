namespace Vls.Shopflow.Catalog.Application.DataTransferObjects;

/// <summary>
/// Compact sales-rule summary for product list cards (not a full per-SKU salesRule).
/// </summary>
public sealed record ProductSalesSummaryDto(
    bool HasUnit,
    bool HasMinimumQuantity,
    bool HasMultipleQuantity,
    bool HasFixedPackage,
    bool HasAssortedPackage,
    bool HasPackage,
    bool IsMixedSalesModes,
    string PrimarySalesMode,
    string? PrimaryBadge,
    int? MinimumQuantity,
    int? QuantityStep,
    int? PackageSize,
    string? PackageLabel,
    string? PackageDescription,
    string? QuantityUnitLabel,
    bool? ShowTotalPieces,
    decimal? PackagePrice,
    decimal? EquivalentUnitPrice,
    decimal? FromPrice,
    string? FromPriceLabel);
