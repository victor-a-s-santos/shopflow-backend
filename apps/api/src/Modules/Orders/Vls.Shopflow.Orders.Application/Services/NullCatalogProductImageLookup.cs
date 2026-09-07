using Vls.Shopflow.Orders.Application.Interfaces;

namespace Vls.Shopflow.Orders.Application.Services;

public sealed class NullCatalogProductImageLookup : ICatalogProductImageLookup
{
    public static NullCatalogProductImageLookup Instance { get; } = new();

    public Task<IReadOnlyDictionary<Guid, string>> GetPrimaryImageUrlsBySkuIdsAsync(
        IReadOnlyCollection<Guid> skuIds,
        CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string>());
}
