using FluentAssertions;
using Vls.Shopflow.BuildingBlocks.Domain.ValueObjects;
using Vls.Shopflow.Catalog.Application.DataTransferObjects;
using Vls.Shopflow.Catalog.Application.Mappers;
using Vls.Shopflow.Catalog.Application.Services;
using Vls.Shopflow.Catalog.Domain.Entities;
using Vls.Shopflow.Catalog.Domain.Enums;
using Vls.Shopflow.Catalog.Domain.ValueObjects;

namespace Vls.Shopflow.Catalog.UnitTests.Application;

/// <summary>
/// §11 testes 27–29, 32–34 — referência de lote (CORSLET 1146).
/// </summary>
public sealed class LoteSalesRuleStorefrontTests
{
    private static Sku CreateCorsletLoteSku(
        bool allowChooseVariants = true,
        SalesMode mode = SalesMode.FixedPackage)
    {
        var productId = Guid.NewGuid();
        var rule = SkuSalesRule.Create(
            mode,
            minimumQuantity: 1,
            quantityStep: 1,
            packageSize: 3,
            packageLabel: "Lote com 3 peças",
            packageDescription: null,
            quantityUnitLabel: "lote(s)",
            allowCustomerToChooseVariants: allowChooseVariants,
            showTotalPieces: true,
            isWholesaleOnly: false);

        return Sku.Create(
            productId,
            "CORSLET-1146-LOTE",
            Price.From(241.00m),
            attributes: null,
            active: true,
            salesRule: rule);
    }

    [Fact]
    public void FixedPackage_Lote_WriteAndRead_ReturnsNormalizedSalesRule()
    {
        // 27 — packageSize=3, quantityUnitLabel=lote(s), regularPrice=241
        var write = new SkuSalesRuleWriteDto(
            "FixedPackage", 1, 1, 3, "Lote com 3 peças", null, "lote(s)", true, true, false);

        var rule = SkuSalesRuleFactory.FromWriteDto(write);
        var dto = SkuSalesRuleFactory.ToDto(rule);

        dto.SalesMode.Should().Be("FixedPackage");
        dto.PackageSize.Should().Be(3);
        dto.PackageLabel.Should().Be("Lote com 3 peças");
        dto.QuantityUnitLabel.Should().Be("lote(s)");
        dto.ShowTotalPieces.Should().BeTrue();
        dto.MinimumQuantity.Should().Be(1);
        dto.QuantityStep.Should().Be(1);
        dto.AllowCustomerToChooseVariants.Should().BeTrue();
    }

    [Fact]
    public void StorefrontSkuDto_Lote_ExposesPackageFieldsAndDisplay()
    {
        // 28 + 29 — shape by-slug / detalhe: salesRule + salesRuleDisplay
        var sku = CreateCorsletLoteSku();
        var dto = SkuDtoMapper.FromEntity(sku);

        dto.RegularPrice.Should().Be(241.00m);
        dto.SalesRule.PackageSize.Should().Be(3);
        dto.SalesRule.PackageLabel.Should().Be("Lote com 3 peças");
        dto.SalesRule.QuantityUnitLabel.Should().Be("lote(s)");
        dto.SalesRule.ShowTotalPieces.Should().BeTrue();

        dto.SalesRuleDisplay.Should().NotBeNull();
        dto.SalesRuleDisplay!.PackageSize.Should().Be(3);
        dto.SalesRuleDisplay.SellingUnitLabel.Should().Be("lote(s)");
        dto.SalesRuleDisplay.PackageSizeLabel.Should().Be("Unidades no lote");
        dto.SalesRuleDisplay.PackagePriceLabel.Should().Be("Preço por lote");
        dto.SalesRuleDisplay.EquivalentRegularUnitPrice.Should().Be(80.33m);
        dto.SalesRuleDisplay.ShowEquivalentUnitPrice.Should().BeTrue();
    }

    [Fact]
    public void FixedPackage_IsNotTreatedAsAssorted()
    {
        // 32
        var sku = CreateCorsletLoteSku(allowChooseVariants: true);
        var dto = SkuDtoMapper.FromEntity(sku);

        dto.SalesRule.SalesMode.Should().Be("FixedPackage");
        dto.SalesRule.AllowCustomerToChooseVariants.Should().BeTrue();
    }

    [Fact]
    public void AssortedPackage_ForcesAllowCustomerToChooseVariantsFalse()
    {
        // 33 — even if write asks for true
        var write = new SkuSalesRuleWriteDto(
            "AssortedPackage", 1, 1, 3, "Lote sortido 3", "Cores sortidas", "lote(s)", true, true, false);

        var dto = SkuSalesRuleFactory.ToDto(SkuSalesRuleFactory.FromWriteDto(write));
        dto.SalesMode.Should().Be("AssortedPackage");
        dto.AllowCustomerToChooseVariants.Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FixedPackage_HonorsAllowCustomerToChooseVariantsAsConfigured(bool allow)
    {
        // 34
        var sku = CreateCorsletLoteSku(allowChooseVariants: allow);
        var dto = SkuDtoMapper.FromEntity(sku);

        dto.SalesRule.SalesMode.Should().Be("FixedPackage");
        dto.SalesRule.AllowCustomerToChooseVariants.Should().Be(allow);
    }
}
