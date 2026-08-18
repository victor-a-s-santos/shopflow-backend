using FluentAssertions;
using FluentValidation;
using Moq;
using Vls.Shopflow.BuildingBlocks.Domain.ValueObjects;
using Vls.Shopflow.Catalog.Application.CommandHandlers;
using Vls.Shopflow.Catalog.Application.Commands;
using Vls.Shopflow.Catalog.Application.DataTransferObjects;
using Vls.Shopflow.Catalog.Application.Interfaces;
using Vls.Shopflow.Catalog.Application.Mappers;
using Vls.Shopflow.Catalog.Application.Repositories;
using Vls.Shopflow.Catalog.Domain.Entities;
using Vls.Shopflow.Catalog.Domain.Enums;
using Vls.Shopflow.Catalog.Domain.ValueObjects;
using Vls.Shopflow.Catalog.Application.Services;

namespace Vls.Shopflow.Catalog.UnitTests.Application;

/// <summary>
/// Update SKU must preserve salesRule when omitted; only explicit payload replaces it.
/// </summary>
public sealed class UpdateSkuSalesRulePreserveTests
{
    private static Mock<IAttributeDefinitionLookup> EmptyLookup() => new();

    private static Mock<ISkuLifecycleGuard> Guard(Guid skuId)
    {
        var guard = new Mock<ISkuLifecycleGuard>();
        guard.Setup(x => x.GetProtectionAsync(skuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SkuProtectionStatus(false, false, false));
        return guard;
    }

    private static (Product Product, Sku Sku, UpdateSkuCommandHandler Handler) Arrange(SkuSalesRule existingRule)
    {
        var product = Product.CreateWithSkus("Corslet", Slug.From("corslet-1146"), null);
        var sku = Sku.Create(product.Id, "CORSLET-1146-LOTE", Price.From(241m), null, true, existingRule);
        product.AddSku(sku);

        var repo = new Mock<IProductRepository>();
        repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        var uow = new Mock<ICatalogUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new UpdateSkuCommandHandler(
            repo.Object, uow.Object, EmptyLookup().Object, Guard(sku.Id).Object);
        return (product, sku, handler);
    }

    private static UpdateSkuCommand PriceOnlyUpdate(Product product, Sku sku, decimal price = 250m)
        => new(product.Id, sku.Id, sku.Code, price, null, [], true, SalesRule: null);

    [Fact]
    public async Task Update_WithoutSalesRule_PreservesUnit()
    {
        var (product, sku, handler) = Arrange(SkuSalesRule.UnitDefault());
        await handler.Handle(PriceOnlyUpdate(product, sku), CancellationToken.None);
        sku.SalesRule.SalesMode.Should().Be(SalesMode.Unit);
        sku.Price.Regular.Amount.Should().Be(250m);
    }

    [Fact]
    public async Task Update_WithoutSalesRule_PreservesMultipleQuantity()
    {
        var rule = SkuSalesRule.Create(SalesMode.MultipleQuantity, 3, 3, null, null, null, null, true, false, false);
        var (product, sku, handler) = Arrange(rule);

        await handler.Handle(PriceOnlyUpdate(product, sku), CancellationToken.None);

        sku.SalesRule.SalesMode.Should().Be(SalesMode.MultipleQuantity);
        sku.SalesRule.MinimumQuantity.Should().Be(3);
        sku.SalesRule.QuantityStep.Should().Be(3);
    }

    [Fact]
    public async Task Update_WithoutSalesRule_PreservesFixedPackage()
    {
        var rule = SkuSalesRule.Create(
            SalesMode.FixedPackage, 1, 1, 3, "Lote com 3 peças", null, "lote(s)", true, true, false);
        var (product, sku, handler) = Arrange(rule);

        await handler.Handle(PriceOnlyUpdate(product, sku), CancellationToken.None);

        sku.SalesRule.SalesMode.Should().Be(SalesMode.FixedPackage);
        sku.SalesRule.PackageSize.Should().Be(3);
        sku.SalesRule.ResolvedQuantityUnitLabel.Should().Be("lote(s)");
    }

    [Fact]
    public async Task Update_WithoutSalesRule_PreservesAssortedPackage()
    {
        var rule = SkuSalesRule.Create(
            SalesMode.AssortedPackage, 1, 1, 6, "Lote sortido", "Cores sortidas", "lote(s)", false, true, false);
        var (product, sku, handler) = Arrange(rule);

        await handler.Handle(PriceOnlyUpdate(product, sku), CancellationToken.None);

        sku.SalesRule.SalesMode.Should().Be(SalesMode.AssortedPackage);
        sku.SalesRule.AllowCustomerToChooseVariants.Should().BeFalse();
        sku.SalesRule.PackageSize.Should().Be(6);
    }

    [Fact]
    public async Task Update_WithExplicitUnit_ResetsSalesRule()
    {
        var rule = SkuSalesRule.Create(
            SalesMode.FixedPackage, 1, 1, 3, "Lote com 3 peças", null, "lote(s)", true, true, false);
        var (product, sku, handler) = Arrange(rule);

        await handler.Handle(
            new UpdateSkuCommand(
                product.Id, sku.Id, sku.Code, 241m, null, [], true,
                new SkuSalesRuleWriteDto("Unit", 1, 1, null, null, null, null, true, false, false)),
            CancellationToken.None);

        sku.SalesRule.SalesMode.Should().Be(SalesMode.Unit);
        sku.SalesRule.PackageSize.Should().BeNull();
    }

    [Fact]
    public async Task Update_WithExplicitMultipleQuantity_ReplacesRule()
    {
        var (product, sku, handler) = Arrange(SkuSalesRule.UnitDefault());

        await handler.Handle(
            new UpdateSkuCommand(
                product.Id, sku.Id, sku.Code, 100m, null, [], true,
                new SkuSalesRuleWriteDto("MultipleQuantity", 3, 3, null, null, null, null, true, false, false)),
            CancellationToken.None);

        sku.SalesRule.SalesMode.Should().Be(SalesMode.MultipleQuantity);
        sku.SalesRule.MinimumQuantity.Should().Be(3);
        sku.SalesRule.QuantityStep.Should().Be(3);
    }

    [Fact]
    public async Task Update_WithEmptySalesMode_ThrowsValidation()
    {
        var (product, sku, handler) = Arrange(
            SkuSalesRule.Create(SalesMode.FixedPackage, 1, 1, 3, "Lote", null, "lote(s)", true, true, false));

        var act = () => handler.Handle(
            new UpdateSkuCommand(
                product.Id, sku.Id, sku.Code, 241m, null, [], true,
                new SkuSalesRuleWriteDto("", 1, 1, null, null, null, null, true, false, false)),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .Where(ex => ex.Errors.Any(e => e.PropertyName.Contains("salesMode", StringComparison.OrdinalIgnoreCase)));

        sku.SalesRule.SalesMode.Should().Be(SalesMode.FixedPackage);
    }

    [Fact]
    public async Task AddSku_WithoutSalesRule_StillDefaultsToUnit()
    {
        var product = Product.CreateWithSkus("Jaqueta", Slug.From("jaqueta"), null);
        var repo = new Mock<IProductRepository>();
        repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        var uow = new Mock<ICatalogUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new AddSkuCommandHandler(repo.Object, uow.Object, EmptyLookup().Object);
        var skuId = await handler.Handle(
            new AddSkuCommand(product.Id, "JKT-M", 10m, null, [], true, SalesRule: null),
            CancellationToken.None);

        product.Skus.Single(s => s.Id == skuId).SalesRule.SalesMode.Should().Be(SalesMode.Unit);
    }

    [Fact]
    public async Task Update_WithoutSalesRule_StorefrontDtoStillExposesPreviousRule()
    {
        var rule = SkuSalesRule.Create(
            SalesMode.FixedPackage, 1, 1, 3, "Lote com 3 peças", null, "lote(s)", true, true, false);
        var (product, sku, handler) = Arrange(rule);

        await handler.Handle(PriceOnlyUpdate(product, sku, 260m), CancellationToken.None);

        var dto = SkuDtoMapper.FromEntity(sku);
        dto.RegularPrice.Should().Be(260m);
        dto.SalesRule.SalesMode.Should().Be("FixedPackage");
        dto.SalesRule.PackageSize.Should().Be(3);
        dto.SalesRuleDisplay.Should().NotBeNull();
        dto.SalesRuleDisplay!.EquivalentRegularUnitPrice.Should().Be(86.67m); // 260/3
    }
}
