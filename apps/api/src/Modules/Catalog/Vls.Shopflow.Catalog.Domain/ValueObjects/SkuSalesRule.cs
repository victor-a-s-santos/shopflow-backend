using Vls.Shopflow.BuildingBlocks.Domain.ValueObjects;
using Vls.Shopflow.Catalog.Domain.Enums;

namespace Vls.Shopflow.Catalog.Domain.ValueObjects;

/// <summary>
/// Commercial sales rule for a SKU. <c>quantity</c> always means units of this SKU
/// (pieces for Unit/Min/Multiple; lotes/pacotes for Fixed/Assorted — never multiply by PackageSize for stock).
/// Domain keeps technical "Package*" names; display uses QuantityUnitLabel / PackageLabel
/// (lote, pacote, kit, caixa, etc.).
/// </summary>
public sealed record SkuSalesRule : ValueObject
{
    public const string DefaultPieceLabel = "peça(s)";
    /// <summary>Default sellable-unit label for package modes (client language: lote).</summary>
    public const string DefaultPackageLabel = "lote(s)";

    public SalesMode SalesMode { get; private set; }
    public int MinimumQuantity { get; private set; }
    public int QuantityStep { get; private set; }
    public int? PackageSize { get; private set; }
    public string? PackageLabel { get; private set; }
    public string? PackageDescription { get; private set; }
    public string? QuantityUnitLabel { get; private set; }
    public bool AllowCustomerToChooseVariants { get; private set; }
    public bool ShowTotalPieces { get; private set; }
    public bool IsWholesaleOnly { get; private set; }

    private SkuSalesRule()
    {
        SalesMode = SalesMode.Unit;
        MinimumQuantity = 1;
        QuantityStep = 1;
        AllowCustomerToChooseVariants = true;
    }

    public static SkuSalesRule UnitDefault()
        => new()
        {
            SalesMode = SalesMode.Unit,
            MinimumQuantity = 1,
            QuantityStep = 1,
            PackageSize = null,
            PackageLabel = null,
            PackageDescription = null,
            QuantityUnitLabel = null,
            AllowCustomerToChooseVariants = true,
            ShowTotalPieces = false,
            IsWholesaleOnly = false
        };

    /// <summary>
    /// Creates a normalized rule. Throws <see cref="ArgumentException"/> on invalid configuration.
    /// </summary>
    public static SkuSalesRule Create(
        SalesMode salesMode,
        int minimumQuantity,
        int quantityStep,
        int? packageSize,
        string? packageLabel,
        string? packageDescription,
        string? quantityUnitLabel,
        bool allowCustomerToChooseVariants,
        bool showTotalPieces,
        bool isWholesaleOnly)
    {
        return salesMode switch
        {
            SalesMode.Unit => NormalizeUnit(isWholesaleOnly),
            SalesMode.MinimumQuantity => NormalizeMinimum(minimumQuantity, quantityUnitLabel, allowCustomerToChooseVariants, isWholesaleOnly),
            SalesMode.MultipleQuantity => NormalizeMultiple(minimumQuantity, quantityStep, quantityUnitLabel, allowCustomerToChooseVariants, isWholesaleOnly),
            SalesMode.FixedPackage => NormalizePackage(
                SalesMode.FixedPackage,
                minimumQuantity,
                quantityStep,
                packageSize,
                packageLabel,
                packageDescription,
                quantityUnitLabel,
                allowCustomerToChooseVariants,
                showTotalPieces,
                isWholesaleOnly,
                forceAllowChooseVariantsFalse: false),
            SalesMode.AssortedPackage => NormalizePackage(
                SalesMode.AssortedPackage,
                minimumQuantity,
                quantityStep,
                packageSize,
                packageLabel,
                packageDescription,
                quantityUnitLabel,
                allowCustomerToChooseVariants: false,
                showTotalPieces,
                isWholesaleOnly,
                forceAllowChooseVariantsFalse: true),
            _ => throw new ArgumentOutOfRangeException(nameof(salesMode), salesMode, "Sales mode is not supported.")
        };
    }

    public bool IsValidPurchaseQuantity(int quantity)
        => quantity > 0
           && quantity >= MinimumQuantity
           && QuantityStep > 0
           && (quantity - MinimumQuantity) % QuantityStep == 0;

    public bool IsPackageMode
        => SalesMode is SalesMode.FixedPackage or SalesMode.AssortedPackage;

    public int? GetDisplayTotalPieces(int quantity)
        => ShowTotalPieces && PackageSize is { } size && size > 0
            ? quantity * size
            : null;

    public string ResolvedQuantityUnitLabel
        => !string.IsNullOrWhiteSpace(QuantityUnitLabel)
            ? QuantityUnitLabel
            : IsPackageMode ? DefaultPackageLabel : DefaultPieceLabel;

    private static SkuSalesRule NormalizeUnit(bool isWholesaleOnly)
        => new()
        {
            SalesMode = SalesMode.Unit,
            MinimumQuantity = 1,
            QuantityStep = 1,
            PackageSize = null,
            PackageLabel = null,
            PackageDescription = null,
            QuantityUnitLabel = null,
            AllowCustomerToChooseVariants = true,
            ShowTotalPieces = false,
            IsWholesaleOnly = isWholesaleOnly
        };

    private static SkuSalesRule NormalizeMinimum(
        int minimumQuantity,
        string? quantityUnitLabel,
        bool allowCustomerToChooseVariants,
        bool isWholesaleOnly)
    {
        if (minimumQuantity <= 1)
            throw new ArgumentException("MinimumQuantity mode requires minimumQuantity > 1.", nameof(minimumQuantity));

        return new SkuSalesRule
        {
            SalesMode = SalesMode.MinimumQuantity,
            MinimumQuantity = minimumQuantity,
            QuantityStep = 1,
            PackageSize = null,
            PackageLabel = null,
            PackageDescription = null,
            QuantityUnitLabel = NullIfWhiteSpace(quantityUnitLabel),
            AllowCustomerToChooseVariants = allowCustomerToChooseVariants,
            ShowTotalPieces = false,
            IsWholesaleOnly = isWholesaleOnly
        };
    }

    private static SkuSalesRule NormalizeMultiple(
        int minimumQuantity,
        int quantityStep,
        string? quantityUnitLabel,
        bool allowCustomerToChooseVariants,
        bool isWholesaleOnly)
    {
        if (quantityStep <= 1)
            throw new ArgumentException("MultipleQuantity mode requires quantityStep > 1.", nameof(quantityStep));

        if (minimumQuantity < 1)
            throw new ArgumentException("minimumQuantity must be >= 1.", nameof(minimumQuantity));

        if (minimumQuantity % quantityStep != 0)
            throw new ArgumentException(
                "MultipleQuantity requires minimumQuantity to be a multiple of quantityStep.",
                nameof(minimumQuantity));

        return new SkuSalesRule
        {
            SalesMode = SalesMode.MultipleQuantity,
            MinimumQuantity = minimumQuantity,
            QuantityStep = quantityStep,
            PackageSize = null,
            PackageLabel = null,
            PackageDescription = null,
            QuantityUnitLabel = NullIfWhiteSpace(quantityUnitLabel),
            AllowCustomerToChooseVariants = allowCustomerToChooseVariants,
            ShowTotalPieces = false,
            IsWholesaleOnly = isWholesaleOnly
        };
    }

    private static SkuSalesRule NormalizePackage(
        SalesMode mode,
        int minimumQuantity,
        int quantityStep,
        int? packageSize,
        string? packageLabel,
        string? packageDescription,
        string? quantityUnitLabel,
        bool allowCustomerToChooseVariants,
        bool showTotalPieces,
        bool isWholesaleOnly,
        bool forceAllowChooseVariantsFalse)
    {
        if (packageSize is null or <= 1)
            throw new ArgumentException("Package modes require packageSize > 1.", nameof(packageSize));

        if (minimumQuantity < 1)
            throw new ArgumentException("minimumQuantity must be >= 1.", nameof(minimumQuantity));

        if (quantityStep < 1)
            throw new ArgumentException("quantityStep must be >= 1.", nameof(quantityStep));

        // Client-facing default: "lote". Admin may override to pacote/kit/caixa via labels.
        var label = NullIfWhiteSpace(packageLabel)
                    ?? $"Lote com {packageSize.Value} peças";

        var unitLabel = NullIfWhiteSpace(quantityUnitLabel) ?? DefaultPackageLabel;

        return new SkuSalesRule
        {
            SalesMode = mode,
            MinimumQuantity = minimumQuantity,
            QuantityStep = quantityStep,
            PackageSize = packageSize,
            PackageLabel = label,
            PackageDescription = NullIfWhiteSpace(packageDescription),
            QuantityUnitLabel = unitLabel,
            AllowCustomerToChooseVariants = forceAllowChooseVariantsFalse ? false : allowCustomerToChooseVariants,
            ShowTotalPieces = showTotalPieces,
            IsWholesaleOnly = isWholesaleOnly
        };
    }

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
