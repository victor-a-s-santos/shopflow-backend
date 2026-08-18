using Vls.Shopflow.Catalog.Application.DataTransferObjects;
using Vls.Shopflow.Catalog.Domain.Enums;
using Vls.Shopflow.Catalog.Domain.ValueObjects;

namespace Vls.Shopflow.Catalog.Application.Services;

/// <summary>
/// Aggregates active SKU sales rules into a list-card summary.
/// Display-only — does not affect checkout/inventory.
/// </summary>
public static class ProductSalesSummaryFactory
{
    public sealed record SkuInput(
        SalesMode SalesMode,
        int MinimumQuantity,
        int QuantityStep,
        int? PackageSize,
        string? PackageLabel,
        string? PackageDescription,
        string? QuantityUnitLabel,
        bool ShowTotalPieces,
        decimal EffectivePrice);

    public static ProductSalesSummaryDto? FromActiveSkus(IReadOnlyList<SkuInput> activeSkus)
    {
        if (activeSkus.Count == 0)
            return null;

        var hasUnit = activeSkus.Any(s => s.SalesMode == SalesMode.Unit);
        var hasMin = activeSkus.Any(s => s.SalesMode == SalesMode.MinimumQuantity);
        var hasMultiple = activeSkus.Any(s => s.SalesMode == SalesMode.MultipleQuantity);
        var hasFixed = activeSkus.Any(s => s.SalesMode == SalesMode.FixedPackage);
        var hasAssorted = activeSkus.Any(s => s.SalesMode == SalesMode.AssortedPackage);
        var hasPackage = hasFixed || hasAssorted;
        var isMixed = activeSkus.Select(s => s.SalesMode).Distinct().Count() > 1;

        var primaryPackage = hasPackage
            ? activeSkus
                .Where(s =>
                    (s.SalesMode is SalesMode.FixedPackage or SalesMode.AssortedPackage)
                    && s.PackageSize is > 1)
                .OrderBy(ComparableUnitPrice)
                .ThenBy(s => s.EffectivePrice)
                .FirstOrDefault()
            : null;

        var fromPrice = activeSkus.Min(ComparableUnitPrice);

        string primarySalesMode;
        string? primaryBadge;
        int? minimumQuantity = null;
        int? quantityStep = null;
        int? packageSize = null;
        string? packageLabel = null;
        string? packageDescription = null;
        string? quantityUnitLabel = null;
        bool? showTotalPieces = null;
        decimal? packagePrice = null;
        decimal? equivalentUnitPrice = null;

        if (primaryPackage is not null)
        {
            packageSize = primaryPackage.PackageSize;
            packagePrice = primaryPackage.EffectivePrice;
            equivalentUnitPrice = SkuSalesRuleFactory.RoundUnitPrice(
                primaryPackage.EffectivePrice,
                primaryPackage.PackageSize!.Value);
            packageLabel = ResolvePackageLabel(primaryPackage);
            packageDescription = string.IsNullOrWhiteSpace(primaryPackage.PackageDescription)
                ? null
                : primaryPackage.PackageDescription.Trim();
            quantityUnitLabel = string.IsNullOrWhiteSpace(primaryPackage.QuantityUnitLabel)
                ? SkuSalesRule.DefaultPackageLabel
                : primaryPackage.QuantityUnitLabel.Trim();
            showTotalPieces = true;
        }

        if (!isMixed)
        {
            if (hasUnit)
            {
                primarySalesMode = nameof(SalesMode.Unit);
                primaryBadge = null;
            }
            else if (hasMin)
            {
                var sku = activeSkus.First(s => s.SalesMode == SalesMode.MinimumQuantity);
                primarySalesMode = nameof(SalesMode.MinimumQuantity);
                minimumQuantity = sku.MinimumQuantity;
                quantityStep = 1;
                primaryBadge = $"Mín. {sku.MinimumQuantity} peças";
            }
            else if (hasMultiple)
            {
                var sku = activeSkus.First(s => s.SalesMode == SalesMode.MultipleQuantity);
                primarySalesMode = nameof(SalesMode.MultipleQuantity);
                minimumQuantity = sku.MinimumQuantity;
                quantityStep = sku.QuantityStep;
                primaryBadge = $"Múltiplos de {sku.QuantityStep}";
            }
            else if (hasFixed)
            {
                primarySalesMode = nameof(SalesMode.FixedPackage);
                primaryBadge = packageLabel ?? $"Lote com {packageSize} peças";
            }
            else
            {
                primarySalesMode = nameof(SalesMode.AssortedPackage);
                primaryBadge = packageLabel ?? $"Lote sortido com {packageSize} peças";
            }
        }
        else
        {
            primarySalesMode = "Mixed";
            if (hasUnit && hasPackage)
                primaryBadge = "Opções por unidade e lote";
            else if (hasUnit && (hasMin || hasMultiple))
                primaryBadge = "Opções de compra flexível";
            else
                primaryBadge = "Opções de compra";

            // Keep package fields for mixed cards that still show lote price.
            if (hasMin || hasMultiple)
            {
                var qtySkus = activeSkus
                    .Where(s => s.SalesMode is SalesMode.MinimumQuantity or SalesMode.MultipleQuantity)
                    .ToList();
                minimumQuantity = qtySkus.Min(s => s.MinimumQuantity);
                var steps = qtySkus
                    .Where(s => s.SalesMode == SalesMode.MultipleQuantity)
                    .Select(s => s.QuantityStep)
                    .ToList();
                quantityStep = steps.Count > 0 ? steps.Min() : null;
            }
        }

        return new ProductSalesSummaryDto(
            HasUnit: hasUnit,
            HasMinimumQuantity: hasMin,
            HasMultipleQuantity: hasMultiple,
            HasFixedPackage: hasFixed,
            HasAssortedPackage: hasAssorted,
            HasPackage: hasPackage,
            IsMixedSalesModes: isMixed,
            PrimarySalesMode: primarySalesMode,
            PrimaryBadge: primaryBadge,
            MinimumQuantity: minimumQuantity,
            QuantityStep: quantityStep,
            PackageSize: packageSize,
            PackageLabel: packageLabel,
            PackageDescription: packageDescription,
            QuantityUnitLabel: quantityUnitLabel,
            ShowTotalPieces: showTotalPieces,
            PackagePrice: packagePrice,
            EquivalentUnitPrice: equivalentUnitPrice,
            FromPrice: fromPrice,
            FromPriceLabel: "A partir de");
    }

    private static decimal ComparableUnitPrice(SkuInput s)
        => s.SalesMode is SalesMode.FixedPackage or SalesMode.AssortedPackage
           && s.PackageSize is > 1
            ? SkuSalesRuleFactory.RoundUnitPrice(s.EffectivePrice, s.PackageSize.Value)
            : s.EffectivePrice;

    private static string ResolvePackageLabel(SkuInput sku)
    {
        if (!string.IsNullOrWhiteSpace(sku.PackageLabel))
            return sku.PackageLabel.Trim();

        var size = sku.PackageSize!.Value;
        return sku.SalesMode == SalesMode.AssortedPackage
            ? $"Lote sortido com {size} peças"
            : $"Lote com {size} peças";
    }
}
