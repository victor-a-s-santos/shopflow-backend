namespace Vls.Shopflow.Catalog.Application.Interfaces;

/// <summary>
/// Cross-module check (inventory + orders) to protect SKUs with stock/history from hard delete / code change.
/// </summary>
public interface ISkuLifecycleGuard
{
    Task<SkuProtectionStatus> GetProtectionAsync(Guid skuId, CancellationToken cancellationToken);
}

public sealed record SkuProtectionStatus(
    bool HasOnHandOrReserved,
    bool HasMovements,
    bool HasOrderHistory)
{
    public bool BlocksHardDelete => HasOnHandOrReserved || HasMovements || HasOrderHistory;
    public bool BlocksCodeChange => BlocksHardDelete;
}
