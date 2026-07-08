namespace Vls.Shopflow.CartCheckout.Application.Interfaces;

public sealed record SkuPricingSnapshot(
    Guid ProductId,
    string ProductName,
    string ProductSlug,
    Guid SkuId,
    string SkuCode,
    decimal UnitPrice,
    bool SkuIsActive,
    bool ProductIsActive);

public interface ICatalogSkuPricingService
{
    Task<SkuPricingSnapshot?> GetBySkuIdAsync(Guid skuId, CancellationToken cancellationToken);
}
