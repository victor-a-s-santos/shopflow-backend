using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.Orders.Application.Interfaces;
using Vls.Shopflow.Orders.Infrastructure;

namespace Vls.Shopflow.Orders.Infrastructure.Services;

public sealed class CatalogProductImageLookup(OrdersDbContext db) : ICatalogProductImageLookup
{
    private sealed class SkuImageRow
    {
        public Guid SkuId { get; init; }
        public string? Url { get; init; }
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetPrimaryImageUrlsBySkuIdsAsync(
        IReadOnlyCollection<Guid> skuIds,
        CancellationToken cancellationToken)
    {
        var ids = skuIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (ids.Length == 0)
            return new Dictionary<Guid, string>();

        var result = new Dictionary<Guid, string>();
        foreach (var skuId in ids)
        {
            var row = await db.Database
                .SqlQuery<SkuImageRow>($"""
                    SELECT
                        s."Id" AS "SkuId",
                        (
                            SELECT img."Url"
                            FROM catalog.product_images img
                            WHERE img."ProductId" = p."Id"
                            ORDER BY img."IsPrimary" DESC, img."SortOrder" ASC
                            LIMIT 1
                        ) AS "Url"
                    FROM catalog.product_skus s
                    INNER JOIN catalog.products p ON p."Id" = s."ProductId"
                    WHERE s."Id" = {skuId}
                    """)
                .FirstOrDefaultAsync(cancellationToken);

            if (row is not null && !string.IsNullOrWhiteSpace(row.Url))
                result[row.SkuId] = row.Url.Trim();
        }

        return result;
    }
}
