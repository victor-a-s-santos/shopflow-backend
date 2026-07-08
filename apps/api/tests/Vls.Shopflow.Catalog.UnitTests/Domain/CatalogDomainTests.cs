using FluentAssertions;
using Vls.Shopflow.BuildingBlocks.Domain.ValueObjects;
using Vls.Shopflow.Catalog.Domain.Entities;
using Vls.Shopflow.Catalog.Domain.ValueObjects;

namespace Vls.Shopflow.Catalog.UnitTests.Domain;

public sealed class ProductTests
{
    [Fact]
    public void CreateWithSkus_AlwaysMarksHasSkusTrue()
    {
        var product = Product.CreateWithSkus("Camiseta", Slug.From("camiseta"), null);

        product.HasSkus.Should().BeTrue();
        product.Skus.Should().BeEmpty();
        product.IsActive.Should().BeTrue();
    }
}

public sealed class SkuTests
{
    [Fact]
    public void AddAttribute_WithDuplicateGlobalDefinition_Throws()
    {
        var definitionId = Guid.NewGuid();
        var valueId1 = Guid.NewGuid();
        var valueId2 = Guid.NewGuid();

        var sku = Sku.Create(
            Guid.NewGuid(),
            "SKU-001",
            Price.From(99.90m),
            [SkuAttribute.FromGlobal(definitionId, valueId1)],
            active: true);

        var act = () => sku.AddAttribute(SkuAttribute.FromGlobal(definitionId, valueId2));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*same global attribute*");
    }

    [Fact]
    public void ReplaceAttributes_WithDuplicateGlobalDefinitions_Throws()
    {
        var definitionId = Guid.NewGuid();
        var sku = Sku.Create(Guid.NewGuid(), "SKU-002", Price.From(50m), null, active: true);

        var act = () => sku.ReplaceAttributes([
            SkuAttribute.FromGlobal(definitionId, Guid.NewGuid()),
            SkuAttribute.FromGlobal(definitionId, Guid.NewGuid())
        ]);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*same global attribute*");
    }

    [Fact]
    public void ReplaceAttributes_UpdatesPriceAndCode()
    {
        var productId = Guid.NewGuid();
        var sku = Sku.Create(productId, "OLD", Price.From(10m), null, active: true);

        sku.ChangeCode("NEW");
        sku.ChangePrice(Price.From(20m, 15m));
        sku.ReplaceAttributes([SkuAttribute.FromCustom("Tamanho", "M")]);

        sku.Code.Should().Be("NEW");
        sku.Price.Regular.Amount.Should().Be(20m);
        sku.Price.Promotional!.Amount.Should().Be(15m);
        sku.Attributes.Should().ContainSingle(a => a.CustomName == "Tamanho" && a.CustomValue == "M");
    }
}
