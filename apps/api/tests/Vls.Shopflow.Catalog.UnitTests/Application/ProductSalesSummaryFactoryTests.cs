using FluentAssertions;
using Vls.Shopflow.Catalog.Application.Services;
using Vls.Shopflow.Catalog.Domain.Enums;
using Xunit;

namespace Vls.Shopflow.Catalog.UnitTests.Application;

public sealed class ProductSalesSummaryFactoryTests
{
    private static ProductSalesSummaryFactory.SkuInput Sku(
        SalesMode mode,
        decimal effectivePrice,
        int min = 1,
        int step = 1,
        int? packageSize = null,
        string? packageLabel = null,
        string? packageDescription = null,
        string? quantityUnitLabel = null,
        bool showTotalPieces = false)
        => new(
            mode,
            min,
            step,
            packageSize,
            packageLabel,
            packageDescription,
            quantityUnitLabel,
            showTotalPieces,
            effectivePrice);

    [Fact]
    public void Empty_skus_returns_null()
    {
        ProductSalesSummaryFactory.FromActiveSkus([]).Should().BeNull();
    }

    [Fact]
    public void Unit_only_has_no_badge()
    {
        var summary = ProductSalesSummaryFactory.FromActiveSkus(
        [
            Sku(SalesMode.Unit, 159.90m),
            Sku(SalesMode.Unit, 179.90m)
        ]);

        summary!.HasUnit.Should().BeTrue();
        summary.HasPackage.Should().BeFalse();
        summary.IsMixedSalesModes.Should().BeFalse();
        summary.PrimarySalesMode.Should().Be("Unit");
        summary.PrimaryBadge.Should().BeNull();
        summary.FromPrice.Should().Be(159.90m);
        summary.FromPriceLabel.Should().Be("A partir de");
        summary.PackagePrice.Should().BeNull();
    }

    [Fact]
    public void MinimumQuantity_badge()
    {
        var summary = ProductSalesSummaryFactory.FromActiveSkus(
        [
            Sku(SalesMode.MinimumQuantity, 50m, min: 3, step: 1)
        ]);

        summary!.PrimarySalesMode.Should().Be("MinimumQuantity");
        summary.PrimaryBadge.Should().Be("Mín. 3 peças");
        summary.MinimumQuantity.Should().Be(3);
        summary.HasMinimumQuantity.Should().BeTrue();
    }

    [Fact]
    public void MultipleQuantity_badge()
    {
        var summary = ProductSalesSummaryFactory.FromActiveSkus(
        [
            Sku(SalesMode.MultipleQuantity, 50m, min: 3, step: 3)
        ]);

        summary!.PrimarySalesMode.Should().Be("MultipleQuantity");
        summary.PrimaryBadge.Should().Be("Múltiplos de 3");
        summary.QuantityStep.Should().Be(3);
        summary.HasMultipleQuantity.Should().BeTrue();
    }

    [Fact]
    public void FixedPackage_prices_and_rounding()
    {
        var summary = ProductSalesSummaryFactory.FromActiveSkus(
        [
            Sku(
                SalesMode.FixedPackage,
                241.00m,
                packageSize: 3,
                packageLabel: "Lote com 3 peças",
                quantityUnitLabel: "lote(s)",
                showTotalPieces: true)
        ]);

        summary!.HasPackage.Should().BeTrue();
        summary.HasFixedPackage.Should().BeTrue();
        summary.PrimarySalesMode.Should().Be("FixedPackage");
        summary.PrimaryBadge.Should().Be("Lote com 3 peças");
        summary.PackageSize.Should().Be(3);
        summary.PackageLabel.Should().Be("Lote com 3 peças");
        summary.QuantityUnitLabel.Should().Be("lote(s)");
        summary.PackagePrice.Should().Be(241.00m);
        summary.EquivalentUnitPrice.Should().Be(80.33m);
        summary.FromPrice.Should().Be(80.33m);
        summary.ShowTotalPieces.Should().BeTrue();
    }

    [Fact]
    public void AssortedPackage_default_badge()
    {
        var summary = ProductSalesSummaryFactory.FromActiveSkus(
        [
            Sku(SalesMode.AssortedPackage, 300m, packageSize: 6)
        ]);

        summary!.HasAssortedPackage.Should().BeTrue();
        summary.PrimarySalesMode.Should().Be("AssortedPackage");
        summary.PrimaryBadge.Should().Be("Lote sortido com 6 peças");
        summary.PackageLabel.Should().Be("Lote sortido com 6 peças");
        summary.PackagePrice.Should().Be(300m);
        summary.EquivalentUnitPrice.Should().Be(50.00m);
    }

    [Fact]
    public void Mixed_unit_and_package_badge()
    {
        var summary = ProductSalesSummaryFactory.FromActiveSkus(
        [
            Sku(SalesMode.Unit, 100m),
            Sku(SalesMode.FixedPackage, 241m, packageSize: 3, packageLabel: "Lote com 3 peças")
        ]);

        summary!.IsMixedSalesModes.Should().BeTrue();
        summary.PrimarySalesMode.Should().Be("Mixed");
        summary.PrimaryBadge.Should().Be("Opções por unidade e lote");
        summary.HasUnit.Should().BeTrue();
        summary.HasPackage.Should().BeTrue();
        summary.PackagePrice.Should().Be(241m);
        summary.EquivalentUnitPrice.Should().Be(80.33m);
        summary.FromPrice.Should().Be(80.33m);
    }

    [Fact]
    public void Mixed_unit_and_minimum_flexible_badge()
    {
        var summary = ProductSalesSummaryFactory.FromActiveSkus(
        [
            Sku(SalesMode.Unit, 40m),
            Sku(SalesMode.MinimumQuantity, 35m, min: 3)
        ]);

        summary!.IsMixedSalesModes.Should().BeTrue();
        summary.PrimaryBadge.Should().Be("Opções de compra flexível");
    }

    [Fact]
    public void Mixed_minimum_and_multiple_without_unit()
    {
        var summary = ProductSalesSummaryFactory.FromActiveSkus(
        [
            Sku(SalesMode.MinimumQuantity, 40m, min: 3),
            Sku(SalesMode.MultipleQuantity, 35m, min: 3, step: 3)
        ]);

        summary!.IsMixedSalesModes.Should().BeTrue();
        summary.PrimarySalesMode.Should().Be("Mixed");
        summary.PrimaryBadge.Should().Be("Opções de compra");
    }

    [Fact]
    public void FromPrice_uses_lowest_comparable_unit_price()
    {
        var summary = ProductSalesSummaryFactory.FromActiveSkus(
        [
            Sku(SalesMode.Unit, 90m),
            Sku(SalesMode.FixedPackage, 241m, packageSize: 3), // 80.33
            Sku(SalesMode.Unit, 85m)
        ]);

        summary!.FromPrice.Should().Be(80.33m);
        summary.PackagePrice.Should().Be(241m);
    }

    [Fact]
    public void Primary_package_is_lowest_equivalent_unit()
    {
        var summary = ProductSalesSummaryFactory.FromActiveSkus(
        [
            Sku(SalesMode.FixedPackage, 300m, packageSize: 3, packageLabel: "Lote A"), // 100
            Sku(SalesMode.FixedPackage, 241m, packageSize: 3, packageLabel: "Lote B")  // 80.33
        ]);

        summary!.PackageLabel.Should().Be("Lote B");
        summary.PackagePrice.Should().Be(241m);
        summary.EquivalentUnitPrice.Should().Be(80.33m);
    }

    [Fact]
    public void PackagePrice_is_sku_price_not_unit()
    {
        var summary = ProductSalesSummaryFactory.FromActiveSkus(
        [
            Sku(SalesMode.FixedPackage, 241m, packageSize: 3)
        ]);

        summary!.PackagePrice.Should().Be(241m);
        summary.EquivalentUnitPrice.Should().NotBe(241m);
    }
}
