namespace Vls.Shopflow.Orders.Application.Interfaces;

public interface ICatalogProductImageLookup
{
    Task<IReadOnlyDictionary<Guid, string>> GetPrimaryImageUrlsBySkuIdsAsync(
        IReadOnlyCollection<Guid> skuIds,
        CancellationToken cancellationToken);
}
