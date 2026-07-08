using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.BuildingBlocks.Domain.ValueObjects;
using Vls.Shopflow.CartCheckout.Application.Interfaces;

namespace Vls.Shopflow.CartCheckout.Infrastructure.Services;

/// <summary>
/// Reads catalog.product_skus via raw SQL (same database, cross-schema) without referencing Catalog domain types.
/// </summary>
public sealed class CatalogSkuPricingService(CartCheckoutDbContext db) : ICatalogSkuPricingService
{
    private sealed class SkuPricingRow
    {
        public Guid ProductId { get; init; }
        public string ProductName { get; init; } = default!;
        public string ProductSlug { get; init; } = default!;
        public Guid SkuId { get; init; }
        public string SkuCode { get; init; } = default!;
        public decimal RegularPrice { get; init; }
        public decimal? PromotionalPrice { get; init; }
        public DateTimeOffset? PromoStart { get; init; }
        public DateTimeOffset? PromoEnd { get; init; }
        public bool SkuIsActive { get; init; }
        public bool ProductIsActive { get; init; }
    }

    public async Task<SkuPricingSnapshot?> GetBySkuIdAsync(Guid skuId, CancellationToken cancellationToken)
    {
        var row = await db.Database
            .SqlQuery<SkuPricingRow>($"""
                SELECT
                    p."Id" AS "ProductId",
                    p."Name" AS "ProductName",
                    p.slug AS "ProductSlug",
                    s."Id" AS "SkuId",
                    s."Code" AS "SkuCode",
                    s.regular_price AS "RegularPrice",
                    s.promo_price AS "PromotionalPrice",
                    s.promo_start AS "PromoStart",
                    s.promo_end AS "PromoEnd",
                    s."IsActive" AS "SkuIsActive",
                    p."IsActive" AS "ProductIsActive"
                FROM catalog.product_skus s
                INNER JOIN catalog.products p ON p."Id" = s."ProductId"
                WHERE s."Id" = {skuId}
                """)
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
            return null;

        var price = Price.From(
            row.RegularPrice,
            row.PromotionalPrice,
            row.PromoStart,
            row.PromoEnd);

        var unitPrice = price.EffectiveNow(DateTimeOffset.UtcNow).Amount;

        return new SkuPricingSnapshot(
            row.ProductId,
            row.ProductName,
            row.ProductSlug,
            row.SkuId,
            row.SkuCode,
            unitPrice,
            row.SkuIsActive,
            row.ProductIsActive);
    }
}
