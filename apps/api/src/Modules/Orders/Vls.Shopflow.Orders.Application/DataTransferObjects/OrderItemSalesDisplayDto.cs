namespace Vls.Shopflow.Orders.Application.DataTransferObjects;

/// <summary>
/// Historical sales-rule display for an order line. Null for Unit/non-package (or legacy rows).
/// Independent of current SKU catalog state.
/// </summary>
public sealed record OrderItemSalesDisplayDto(
    string SalesMode,
    int? PackageSize,
    string? PackageLabel,
    string? PackageDescription,
    string? QuantityUnitLabel,
    bool ShowTotalPieces,
    int? TotalPieces,
    decimal? EquivalentUnitPrice,
    string? Summary);
