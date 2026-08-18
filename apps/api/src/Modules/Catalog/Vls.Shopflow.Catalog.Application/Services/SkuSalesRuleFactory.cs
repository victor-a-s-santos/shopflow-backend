using FluentValidation;
using FluentValidation.Results;
using Vls.Shopflow.Catalog.Application.DataTransferObjects;
using Vls.Shopflow.Catalog.Domain.Enums;
using Vls.Shopflow.Catalog.Domain.ValueObjects;

namespace Vls.Shopflow.Catalog.Application.Services;

public static class SkuSalesRuleFactory
{
    public static SkuSalesRule FromWriteDto(SkuSalesRuleWriteDto? dto, string propertyPrefix = "salesRule")
    {
        // null only on create/add → Unit. Update must not call this with null.
        if (dto is null)
            return SkuSalesRule.UnitDefault();

        // Present payload with empty mode is invalid (never silent Unit reset).
        if (string.IsNullOrWhiteSpace(dto.SalesMode))
        {
            throw new ValidationException([
                new ValidationFailure($"{propertyPrefix}.salesMode", "Informe o modo de venda (salesMode).")
                {
                    ErrorCode = "SALES_RULE_INVALID_CONFIGURATION"
                }
            ]);
        }

        if (!Enum.TryParse<SalesMode>(dto.SalesMode.Trim(), ignoreCase: true, out var mode)
            || !Enum.IsDefined(mode))
        {
            throw new ValidationException([
                new ValidationFailure($"{propertyPrefix}.salesMode", "Modo de venda inválido.")
                {
                    ErrorCode = "SALES_RULE_INVALID_CONFIGURATION"
                }
            ]);
        }

        try
        {
            var isPackage = mode is SalesMode.FixedPackage or SalesMode.AssortedPackage;
            return SkuSalesRule.Create(
                mode,
                dto.MinimumQuantity ?? (mode == SalesMode.MinimumQuantity ? 0 : 1),
                dto.QuantityStep ?? (mode == SalesMode.MultipleQuantity ? 0 : 1),
                dto.PackageSize,
                dto.PackageLabel,
                dto.PackageDescription,
                dto.QuantityUnitLabel,
                dto.AllowCustomerToChooseVariants ?? true,
                dto.ShowTotalPieces ?? isPackage,
                dto.IsWholesaleOnly ?? false);
        }
        catch (ArgumentException ex)
        {
            var field = GuessField(ex.ParamName, propertyPrefix);
            throw new ValidationException([
                new ValidationFailure(field, MapMessage(ex))
                {
                    ErrorCode = "SALES_RULE_INVALID_CONFIGURATION"
                }
            ]);
        }
    }

    public static SkuSalesRuleDto ToDto(SkuSalesRule rule)
        => new(
            rule.SalesMode.ToString(),
            rule.MinimumQuantity,
            rule.QuantityStep,
            rule.PackageSize,
            rule.PackageLabel,
            rule.PackageDescription,
            rule.ResolvedQuantityUnitLabel,
            rule.AllowCustomerToChooseVariants,
            rule.ShowTotalPieces,
            rule.IsWholesaleOnly);

    /// <summary>
    /// Computed storefront display for Fixed/Assorted package SKUs.
    /// Equivalent unit prices round to 2 decimals with <see cref="MidpointRounding.AwayFromZero"/> (BRL).
    /// </summary>
    public static SkuSalesRuleDisplayDto? ToDisplayDto(
        SkuSalesRule rule,
        decimal regularPrice,
        decimal? promotionalPrice)
    {
        if (!rule.IsPackageMode || rule.PackageSize is not { } size || size <= 1)
            return null;

        var sellingUnitLabel = rule.ResolvedQuantityUnitLabel;
        var singular = ToSingularUnit(sellingUnitLabel);

        return new SkuSalesRuleDisplayDto(
            SellingUnitLabel: sellingUnitLabel,
            PackageSize: size,
            PackageSizeLabel: $"Unidades no {singular}",
            PackagePriceLabel: $"Preço por {singular}",
            EquivalentUnitPriceLabel: "Valor unitário",
            ShowEquivalentUnitPrice: true,
            EquivalentRegularUnitPrice: RoundUnitPrice(regularPrice, size),
            EquivalentPromotionalUnitPrice: promotionalPrice is { } promo
                ? RoundUnitPrice(promo, size)
                : null);
    }

    public static decimal RoundUnitPrice(decimal skuPrice, int packageSize)
        => Math.Round(skuPrice / packageSize, 2, MidpointRounding.AwayFromZero);

    /// <summary>"lote(s)" → "lote"; "pacote(s)" → "pacote"; already singular → unchanged.</summary>
    internal static string ToSingularUnit(string quantityUnitLabel)
    {
        var trimmed = quantityUnitLabel.Trim();
        if (trimmed.EndsWith("(s)", StringComparison.OrdinalIgnoreCase))
            return trimmed[..^3].TrimEnd();
        if (trimmed.EndsWith("s", StringComparison.OrdinalIgnoreCase) && trimmed.Length > 1)
            return trimmed[..^1];
        return trimmed;
    }

    private static string GuessField(string? paramName, string prefix)
        => paramName switch
        {
            "minimumQuantity" or "MinimumQuantity" => $"{prefix}.minimumQuantity",
            "quantityStep" or "QuantityStep" => $"{prefix}.quantityStep",
            "packageSize" or "PackageSize" => $"{prefix}.packageSize",
            "packageLabel" or "PackageLabel" => $"{prefix}.packageLabel",
            "salesMode" or "SalesMode" => $"{prefix}.salesMode",
            _ => $"{prefix}"
        };

    private static string MapMessage(ArgumentException ex)
    {
        if (ex.Message.Contains("minimumQuantity > 1", StringComparison.OrdinalIgnoreCase))
            return "No modo MinimumQuantity, a quantidade mínima deve ser maior que 1.";
        if (ex.Message.Contains("quantityStep > 1", StringComparison.OrdinalIgnoreCase))
            return "No modo MultipleQuantity, o incremento deve ser maior que 1.";
        if (ex.Message.Contains("multiple of quantityStep", StringComparison.OrdinalIgnoreCase))
            return "No modo MultipleQuantity, a quantidade mínima deve ser múltiplo do incremento.";
        if (ex.Message.Contains("packageSize > 1", StringComparison.OrdinalIgnoreCase))
            return "Lotes/pacotes exigem packageSize maior que 1.";
        if (ex.Message.Contains("not supported", StringComparison.OrdinalIgnoreCase))
            return "Modo de venda não suportado.";
        return "Configuração de regra de venda inválida.";
    }
}
