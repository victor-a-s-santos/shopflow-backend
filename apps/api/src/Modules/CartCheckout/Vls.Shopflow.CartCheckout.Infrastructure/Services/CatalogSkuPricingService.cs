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
        public int SalesMode { get; init; }
        public int MinimumQuantity { get; init; }
        public int QuantityStep { get; init; }
        public int? PackageSize { get; init; }
        public string? PackageLabel { get; init; }
        public string? PackageDescription { get; init; }
        public string? QuantityUnitLabel { get; init; }
        public bool ShowTotalPieces { get; init; }
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
                    p."IsActive" AS "ProductIsActive",
                    COALESCE(s.sales_mode, 0) AS "SalesMode",
                    COALESCE(s.minimum_quantity, 1) AS "MinimumQuantity",
                    COALESCE(s.quantity_step, 1) AS "QuantityStep",
                    s.package_size AS "PackageSize",
                    s.package_label AS "PackageLabel",
                    s.package_description AS "PackageDescription",
                    s.quantity_unit_label AS "QuantityUnitLabel",
                    COALESCE(s.show_total_pieces, FALSE) AS "ShowTotalPieces"
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
        var salesModeName = row.SalesMode switch
        {
            1 => "MinimumQuantity",
            2 => "MultipleQuantity",
            3 => "FixedPackage",
            4 => "AssortedPackage",
            _ => "Unit"
        };
        var isPackage = row.SalesMode is 3 or 4;

        return new SkuPricingSnapshot(
            row.ProductId,
            row.ProductName,
            row.ProductSlug,
            row.SkuId,
            row.SkuCode,
            unitPrice,
            row.SkuIsActive,
            row.ProductIsActive,
            new SkuSalesRuleSnapshot(
                salesModeName,
                row.MinimumQuantity <= 0 ? 1 : row.MinimumQuantity,
                row.QuantityStep <= 0 ? 1 : row.QuantityStep,
                row.PackageSize,
                isPackage,
                row.PackageLabel,
                row.PackageDescription,
                row.QuantityUnitLabel,
                row.ShowTotalPieces));
    }
}
