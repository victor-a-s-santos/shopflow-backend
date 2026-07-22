using FluentAssertions;
using FluentValidation;
using Vls.Shopflow.Catalog.Application.DataTransferObjects;
using Vls.Shopflow.Catalog.Application.Services;
using Vls.Shopflow.Catalog.Domain.Enums;
using Vls.Shopflow.Catalog.Domain.ValueObjects;
using Vls.Shopflow.BuildingBlocks.Domain.ValueObjects;
using Vls.Shopflow.Catalog.Domain.Entities;

namespace Vls.Shopflow.Catalog.UnitTests.Domain;

public sealed class SkuSalesRuleTests
{
    [Fact]
    public void FromWriteDto_Null_BecomesUnitDefault()
    {
        var rule = SkuSalesRuleFactory.FromWriteDto(null);
        rule.SalesMode.Should().Be(SalesMode.Unit);
        rule.MinimumQuantity.Should().Be(1);
        rule.QuantityStep.Should().Be(1);
        rule.PackageSize.Should().BeNull();
    }

    [Fact]
    public void FromWriteDto_EmptySalesMode_ThrowsValidation()
    {
        var act = () => SkuSalesRuleFactory.FromWriteDto(
            new SkuSalesRuleWriteDto("", 1, 1, null, null, null, null, true, false, false));
        act.Should().Throw<ValidationException>()
            .Which.Errors.Should().Contain(e => e.PropertyName.Contains("salesMode"));
    }

    [Fact]
    public void Unit_IsValid()
    {
        var rule = SkuSalesRule.Create(SalesMode.Unit, 1, 1, null, null, null, null, true, false, false);
        rule.IsValidPurchaseQuantity(1).Should().BeTrue();
        rule.IsValidPurchaseQuantity(5).Should().BeTrue();
    }

    [Fact]
    public void MinimumQuantity_ValidAndInvalid()
    {
        var rule = SkuSalesRule.Create(SalesMode.MinimumQuantity, 3, 1, null, null, null, null, true, false, false);
        rule.IsValidPurchaseQuantity(2).Should().BeFalse();
        rule.IsValidPurchaseQuantity(3).Should().BeTrue();
        rule.IsValidPurchaseQuantity(4).Should().BeTrue();

        var act = () => SkuSalesRule.Create(SalesMode.MinimumQuantity, 1, 1, null, null, null, null, true, false, false);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MultipleQuantity_ValidMinStep()
    {
        var rule = SkuSalesRule.Create(SalesMode.MultipleQuantity, 3, 3, null, null, null, null, true, false, false);
        rule.IsValidPurchaseQuantity(3).Should().BeTrue();
        rule.IsValidPurchaseQuantity(6).Should().BeTrue();
        rule.IsValidPurchaseQuantity(4).Should().BeFalse();
    }

    [Fact]
    public void MultipleQuantity_InvalidConfig_MinNotMultipleOfStep()
    {
        var act = () => SkuSalesRule.Create(SalesMode.MultipleQuantity, 4, 3, null, null, null, null, true, false, false);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MultipleQuantity_StepMustBeGreaterThanOne()
    {
        var act = () => SkuSalesRule.Create(SalesMode.MultipleQuantity, 3, 1, null, null, null, null, true, false, false);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FixedPackage_GeneratesDefaultLoteLabel()
    {
        var rule = SkuSalesRule.Create(
            SalesMode.FixedPackage, 1, 1, 6, null, null, null, true, true, false);
        rule.PackageSize.Should().Be(6);
        rule.PackageLabel.Should().Be("Lote com 6 peças");
        rule.ResolvedQuantityUnitLabel.Should().Be("lote(s)");
        rule.ShowTotalPieces.Should().BeTrue();
        rule.GetDisplayTotalPieces(2).Should().Be(12);
    }

    [Fact]
    public void FixedPackage_AcceptsCustomLoteTerminology()
    {
        // Client reference: CORSLET 1146 — 3 pieces/lote, buy 2 lotes → 6 pieces display.
        var rule = SkuSalesRule.Create(
            SalesMode.FixedPackage, 1, 1, 3, "Lote com 3 peças", null, "lote(s)", true, true, false);
        rule.PackageSize.Should().Be(3);
        rule.PackageLabel.Should().Be("Lote com 3 peças");
        rule.ResolvedQuantityUnitLabel.Should().Be("lote(s)");
        rule.GetDisplayTotalPieces(2).Should().Be(6);
        rule.IsValidPurchaseQuantity(2).Should().BeTrue();
    }

    [Theory]
    [InlineData("pacote(s)", "Pacote com 6 peças")]
    [InlineData("kit(s)", "Kit com 6 peças")]
    [InlineData("caixa(s)", "Caixa com 6 peças")]
    public void Package_AcceptsBusinessUnitLabels(string unitLabel, string packageLabel)
    {
        var rule = SkuSalesRule.Create(
            SalesMode.FixedPackage, 1, 1, 6, packageLabel, null, unitLabel, true, true, false);
        rule.ResolvedQuantityUnitLabel.Should().Be(unitLabel);
        rule.PackageLabel.Should().Be(packageLabel);
    }

    [Fact]
    public void AssortedPackage_ForcesAllowChooseVariantsFalse()
    {
        var rule = SkuSalesRule.Create(
            SalesMode.AssortedPackage, 1, 1, 12, "Lote sortido 12", "Cores sortidas", null, true, true, false);
        rule.AllowCustomerToChooseVariants.Should().BeFalse();
        rule.IsPackageMode.Should().BeTrue();
        rule.ResolvedQuantityUnitLabel.Should().Be("lote(s)");
    }

    [Fact]
    public void FixedPackage_IsNotAssumedAssorted()
    {
        var rule = SkuSalesRule.Create(
            SalesMode.FixedPackage, 1, 1, 3, "Lote com 3 peças", null, "lote(s)", true, true, false);
        rule.SalesMode.Should().Be(SalesMode.FixedPackage);
        rule.AllowCustomerToChooseVariants.Should().BeTrue();
    }

    [Fact]
    public void Package_SizeMustBeGreaterThanOne()
    {
        var act = () => SkuSalesRule.Create(
            SalesMode.FixedPackage, 1, 1, 1, "x", null, null, true, true, false);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Factory_InvalidMode_ThrowsValidationException()
    {
        var act = () => SkuSalesRuleFactory.FromWriteDto(
            new SkuSalesRuleWriteDto("ClosedGrid", 1, 1, null, null, null, null, true, false, false));
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void Sku_CreateWithoutSalesRule_DefaultsToUnit()
    {
        var sku = Sku.Create(Guid.NewGuid(), "A", Price.From(10m), null, true);
        sku.SalesRule.SalesMode.Should().Be(SalesMode.Unit);
        sku.SalesRule.MinimumQuantity.Should().Be(1);
    }

    [Fact]
    public void ToDto_ReturnsNormalizedLabels()
    {
        var rule = SkuSalesRule.UnitDefault();
        var dto = SkuSalesRuleFactory.ToDto(rule);
        dto.SalesMode.Should().Be("Unit");
        dto.QuantityUnitLabel.Should().Be("peça(s)");
        dto.MinimumQuantity.Should().Be(1);
        dto.QuantityStep.Should().Be(1);
    }

    [Fact]
    public void ToDisplayDto_UnitMode_ReturnsNull()
    {
        var display = SkuSalesRuleFactory.ToDisplayDto(SkuSalesRule.UnitDefault(), 100m, null);
        display.Should().BeNull();
    }

    [Fact]
    public void ToDisplayDto_FixedPackage_ComputesEquivalentUnitPrice_CorsletExample()
    {
        // CORSLET 1146: R$ 241,00 / 3 = R$ 80,33
        var rule = SkuSalesRule.Create(
            SalesMode.FixedPackage, 1, 1, 3, "Lote com 3 peças", null, "lote(s)", true, true, false);

        var display = SkuSalesRuleFactory.ToDisplayDto(rule, 241.00m, null);

        display.Should().NotBeNull();
        display!.SellingUnitLabel.Should().Be("lote(s)");
        display.PackageSize.Should().Be(3);
        display.PackageSizeLabel.Should().Be("Unidades no lote");
        display.PackagePriceLabel.Should().Be("Preço por lote");
        display.EquivalentUnitPriceLabel.Should().Be("Valor unitário");
        display.ShowEquivalentUnitPrice.Should().BeTrue();
        display.EquivalentRegularUnitPrice.Should().Be(80.33m);
        display.EquivalentPromotionalUnitPrice.Should().BeNull();
    }

    [Fact]
    public void ToDisplayDto_WithPromo_ComputesBothEquivalents()
    {
        var rule = SkuSalesRule.Create(
            SalesMode.FixedPackage, 1, 1, 3, "Lote com 3 peças", null, "lote(s)", true, true, false);

        var display = SkuSalesRuleFactory.ToDisplayDto(rule, 241.00m, 210.00m);

        display!.EquivalentRegularUnitPrice.Should().Be(80.33m);
        display.EquivalentPromotionalUnitPrice.Should().Be(70.00m);
    }

    [Fact]
    public void ToDisplayDto_PacoteLabel_UsesPacoteCopy()
    {
        var rule = SkuSalesRule.Create(
            SalesMode.AssortedPackage, 1, 1, 6, "Pacote sortido", null, "pacote(s)", false, true, false);

        var display = SkuSalesRuleFactory.ToDisplayDto(rule, 120m, null);
        display!.PackageSizeLabel.Should().Be("Unidades no pacote");
        display.PackagePriceLabel.Should().Be("Preço por pacote");
        display.EquivalentRegularUnitPrice.Should().Be(20.00m);
    }
}
