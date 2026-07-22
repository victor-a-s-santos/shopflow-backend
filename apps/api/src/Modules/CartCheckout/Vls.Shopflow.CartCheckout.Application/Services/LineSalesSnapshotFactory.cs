using Vls.Shopflow.CartCheckout.Application.Interfaces;
using Vls.Shopflow.CartCheckout.Domain.Entities;

namespace Vls.Shopflow.CartCheckout.Application.Services;

/// <summary>
/// Captures sales-rule display snapshot for a checkout line (display only; not used for stock/payment).
/// </summary>
public static class LineSalesSnapshotFactory
{
    public static CheckoutItemSalesSnapshot Capture(SkuSalesRuleSnapshot rule, int quantity, decimal unitPrice)
    {
        ArgumentNullException.ThrowIfNull(rule);

        if (!rule.IsPackageMode || rule.PackageSize is not { } size || size <= 1)
        {
            return new CheckoutItemSalesSnapshot(
                SalesMode: string.IsNullOrWhiteSpace(rule.SalesMode) ? "Unit" : rule.SalesMode,
                PackageSize: null,
                PackageLabel: null,
                PackageDescription: null,
                QuantityUnitLabel: ResolvePieceLabel(rule),
                ShowTotalPieces: false,
                TotalPieces: null,
                EquivalentUnitPrice: null,
                SalesDisplaySummary: null);
        }

        var unitLabel = ResolvePackageUnitLabel(rule);
        var packageLabel = string.IsNullOrWhiteSpace(rule.PackageLabel)
            ? $"Lote com {size} peças"
            : rule.PackageLabel.Trim();
        var totalPieces = quantity * size;
        var equivalent = Math.Round(unitPrice / size, 2, MidpointRounding.AwayFromZero);
        var summary = $"{quantity} {unitLabel} = {totalPieces} peças";

        return new CheckoutItemSalesSnapshot(
            SalesMode: rule.SalesMode,
            PackageSize: size,
            PackageLabel: packageLabel,
            PackageDescription: string.IsNullOrWhiteSpace(rule.PackageDescription)
                ? null
                : rule.PackageDescription.Trim(),
            QuantityUnitLabel: unitLabel,
            ShowTotalPieces: true,
            TotalPieces: totalPieces,
            EquivalentUnitPrice: equivalent,
            SalesDisplaySummary: summary);
    }

    private static string ResolvePieceLabel(SkuSalesRuleSnapshot rule)
        => string.IsNullOrWhiteSpace(rule.QuantityUnitLabel) ? "peça(s)" : rule.QuantityUnitLabel.Trim();

    private static string ResolvePackageUnitLabel(SkuSalesRuleSnapshot rule)
        => string.IsNullOrWhiteSpace(rule.QuantityUnitLabel) ? "lote(s)" : rule.QuantityUnitLabel.Trim();
}
