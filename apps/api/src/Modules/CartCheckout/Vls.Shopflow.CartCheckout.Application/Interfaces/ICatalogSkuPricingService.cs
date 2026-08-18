namespace Vls.Shopflow.CartCheckout.Application.Interfaces;

public sealed record SkuSalesRuleSnapshot(
    string SalesMode,
    int MinimumQuantity,
    int QuantityStep,
    int? PackageSize,
    bool IsPackageMode,
    string? PackageLabel = null,
    string? PackageDescription = null,
    string? QuantityUnitLabel = null,
    bool ShowTotalPieces = false);

public sealed record SkuPricingSnapshot(
    Guid ProductId,
    string ProductName,
    string ProductSlug,
    Guid SkuId,
    string SkuCode,
    decimal UnitPrice,
    bool SkuIsActive,
    bool ProductIsActive,
    SkuSalesRuleSnapshot SalesRule);

public interface ICatalogSkuPricingService
{
    Task<SkuPricingSnapshot?> GetBySkuIdAsync(Guid skuId, CancellationToken cancellationToken);
}
