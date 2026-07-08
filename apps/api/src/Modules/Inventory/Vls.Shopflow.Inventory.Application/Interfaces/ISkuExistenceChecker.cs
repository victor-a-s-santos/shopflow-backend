namespace Vls.Shopflow.Inventory.Application.Interfaces;

/// <summary>
/// Anti-corruption read port: validates SKU existence in Catalog without coupling Inventory domain to Catalog entities.
/// </summary>
public interface ISkuExistenceChecker
{
    Task<bool> ExistsAsync(Guid skuId, CancellationToken cancellationToken);
}
