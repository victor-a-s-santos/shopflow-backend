using Vls.Shopflow.Orders.Application.Interfaces;
using Vls.Shopflow.Orders.Domain.Entities;

namespace Vls.Shopflow.Orders.Application.Services;

internal static class OrderItemImageUrl
{
    public static string? Coalesce(
        string? snapshotUrl,
        Guid skuId,
        IReadOnlyDictionary<Guid, string>? catalogImages)
    {
        if (!string.IsNullOrWhiteSpace(snapshotUrl))
            return snapshotUrl.Trim();

        if (catalogImages is not null
            && catalogImages.TryGetValue(skuId, out var url)
            && !string.IsNullOrWhiteSpace(url))
            return url.Trim();

        return null;
    }

    public static IReadOnlyCollection<Guid> MissingSkuIds(IEnumerable<OrderItem> items)
        => items
            .Where(i => string.IsNullOrWhiteSpace(i.ProductImageUrl))
            .Select(i => i.SkuId)
            .Distinct()
            .ToArray();

    public static Task<IReadOnlyDictionary<Guid, string>> LookupMissingAsync(
        ICatalogProductImageLookup lookup,
        IEnumerable<OrderItem> items,
        CancellationToken cancellationToken)
        => lookup.GetPrimaryImageUrlsBySkuIdsAsync(MissingSkuIds(items), cancellationToken);
}
