using FluentAssertions;
using FluentValidation;
using Moq;
using Vls.Shopflow.BuildingBlocks.Domain.ValueObjects;
using Vls.Shopflow.Catalog.Application.CommandHandlers;
using Vls.Shopflow.Catalog.Application.Commands;
using Vls.Shopflow.Catalog.Application.DataTransferObjects;
using Vls.Shopflow.Catalog.Application.Interfaces;
using Vls.Shopflow.Catalog.Application.QueryHandlers;
using Vls.Shopflow.Catalog.Application.Queries;
using Vls.Shopflow.Catalog.Application.Repositories;
using Vls.Shopflow.Catalog.Application.Services;
using Vls.Shopflow.Catalog.Application.Validations;
using Vls.Shopflow.Catalog.Domain.Entities;
using Vls.Shopflow.Catalog.Domain.Exceptions;
using Vls.Shopflow.Catalog.Domain.ValueObjects;

namespace Vls.Shopflow.Catalog.UnitTests.Application;

public sealed class QueryHandlerTests
{
    [Fact]
    public async Task GetProductBySlug_ReturnsDtoFromReadModel()
    {
        var slug = "camiseta-basica";
        var dto = new ProductDetailedDto(
            Guid.NewGuid(),
            "Camiseta",
            slug,
            true,
            null,
            null,
            true,
            99.90m,
            null,
            99.90m,
            [],
            []);

        var readModel = new Mock<IProductReadModel>();
        readModel.Setup(x => x.GetBySlugAsync(slug, It.IsAny<CancellationToken>())).ReturnsAsync(dto);

        var handler = new GetProductBySlugQueryHandler(readModel.Object);
        var result = await handler.Handle(new GetProductBySlugQuery(slug), CancellationToken.None);

        result.Should().Be(dto);
    }
}

public sealed class CommandHandlerTests
{
    [Fact]
    public async Task AddSku_ToVariantProduct_PersistsSku()
    {
        var product = Product.CreateWithSkus("Jaqueta", Slug.From("jaqueta"), null);
        var repo = new Mock<IProductRepository>();
        repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var uow = new Mock<ICatalogUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var lookup = EmptyLookup();
        var handler = new AddSkuCommandHandler(repo.Object, uow.Object, lookup.Object);
        var skuId = await handler.Handle(
            new AddSkuCommand(product.Id, "JKT-M", 199.90m, null, [], true),
            CancellationToken.None);

        skuId.Should().NotBeEmpty();
        product.Skus.Should().ContainSingle(s => s.Id == skuId && s.Code == "JKT-M");
        uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddSku_WithActiveFalse_PersistsInactive()
    {
        var product = Product.CreateWithSkus("Jaqueta", Slug.From("jaqueta"), null);
        var repo = new Mock<IProductRepository>();
        repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        var uow = new Mock<ICatalogUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new AddSkuCommandHandler(repo.Object, uow.Object, EmptyLookup().Object);
        var skuId = await handler.Handle(
            new AddSkuCommand(product.Id, "JKT-OFF", 10m, null, [], Active: false),
            CancellationToken.None);

        product.Skus.Should().ContainSingle(s => s.Id == skuId && !s.IsActive);
    }

    [Fact]
    public async Task UpdateSku_ActiveFalse_DeactivatesSku()
    {
        var product = Product.CreateWithSkus("Jaqueta", Slug.From("jaqueta"), null);
        var sku = Sku.Create(product.Id, "JKT-S", Price.From(10m), null, true);
        product.AddSku(sku);

        var repo = new Mock<IProductRepository>();
        repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        var uow = new Mock<ICatalogUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var guard = new Mock<ISkuLifecycleGuard>();
        guard.Setup(x => x.GetProtectionAsync(sku.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SkuProtectionStatus(false, false, false));

        var handler = new UpdateSkuCommandHandler(repo.Object, uow.Object, EmptyLookup().Object, guard.Object);
        await handler.Handle(
            new UpdateSkuCommand(product.Id, sku.Id, "JKT-S", 10m, null, [], Active: false),
            CancellationToken.None);

        sku.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task AddSku_CustomAttributeVariadas_PersistsCustomNameOnDefinition()
    {
        var product = Product.CreateWithSkus("Camiseta", Slug.From("camiseta"), null);
        var defId = Guid.NewGuid();
        var lookup = LookupWithCustom(defId, "Cor");

        var repo = new Mock<IProductRepository>();
        repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        var uow = new Mock<ICatalogUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new AddSkuCommandHandler(repo.Object, uow.Object, lookup.Object);
        var skuId = await handler.Handle(
            new AddSkuCommand(
                product.Id,
                "CAM-VAR",
                99.9m,
                null,
                [new SkuAttributeCreateDto(defId, null, "Variadas", null)],
                true),
            CancellationToken.None);

        var sku = product.Skus.Single(s => s.Id == skuId);
        sku.Attributes.Should().ContainSingle(a =>
            a.AttributeDefinitionId == defId &&
            a.CustomName == "Variadas" &&
            a.AttributeValueDefinitionId == null);
    }

    [Fact]
    public async Task AddSku_EmptyCode_GeneratesUniqueFromProductName()
    {
        var product = Product.CreateWithSkus("Conjunto Flores", Slug.From("conjunto-flores"), null);
        var repo = new Mock<IProductRepository>();
        repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        var uow = new Mock<ICatalogUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new AddSkuCommandHandler(repo.Object, uow.Object, EmptyLookup().Object);
        var id1 = await handler.Handle(new AddSkuCommand(product.Id, null, 10m, null, [], true), CancellationToken.None);
        var id2 = await handler.Handle(new AddSkuCommand(product.Id, "  ", 10m, null, [], true), CancellationToken.None);

        var codes = product.Skus.Select(s => s.Code).ToList();
        codes.Should().HaveCount(2);
        codes.Should().OnlyHaveUniqueItems();
        codes.Should().AllSatisfy(c => c.Should().StartWith("CONJUNTO-FLORES"));
        id1.Should().NotBe(id2);
    }

    [Fact]
    public async Task AddSku_DuplicateCode_ThrowsConflict()
    {
        var product = Product.CreateWithSkus("Jaqueta", Slug.From("jaqueta"), null);
        product.AddSku(Sku.Create(product.Id, "JKT-M", Price.From(10m), null, true));

        var repo = new Mock<IProductRepository>();
        repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var handler = new AddSkuCommandHandler(repo.Object, Mock.Of<ICatalogUnitOfWork>(), EmptyLookup().Object);
        var act = () => handler.Handle(
            new AddSkuCommand(product.Id, "jkt-m", 20m, null, [], true),
            CancellationToken.None);

        await act.Should().ThrowAsync<CatalogConflictException>()
            .Where(e => e.ErrorCode == CatalogErrorCodes.SkuCodeDuplicate && e.Field == "code");
    }

    [Fact]
    public async Task UpdateSku_ChangesExistingSku()
    {
        var product = Product.CreateWithSkus("Jaqueta", Slug.From("jaqueta"), null);
        var sku = Sku.Create(product.Id, "JKT-S", Price.From(199.90m), null, true);
        product.AddSku(sku);

        var defId = Guid.NewGuid();
        var lookup = LookupWithCustom(defId, "Tamanho");

        var repo = new Mock<IProductRepository>();
        repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var uow = new Mock<ICatalogUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var guard = new Mock<ISkuLifecycleGuard>();
        guard.Setup(x => x.GetProtectionAsync(sku.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SkuProtectionStatus(false, false, false));

        var handler = new UpdateSkuCommandHandler(repo.Object, uow.Object, lookup.Object, guard.Object);
        await handler.Handle(
            new UpdateSkuCommand(
                product.Id,
                sku.Id,
                "JKT-M",
                249.90m,
                199.90m,
                [new SkuAttributeCreateDto(defId, null, "M", null)],
                true),
            CancellationToken.None);

        sku.Code.Should().Be("JKT-M");
        sku.Price.Regular.Amount.Should().Be(249.90m);
        sku.Attributes.Should().ContainSingle(a =>
            a.AttributeDefinitionId == defId && a.CustomName == "M");
    }

    [Fact]
    public async Task DeleteSku_WhenProtected_ThrowsConflict()
    {
        var product = Product.CreateWithSkus("Jaqueta", Slug.From("jaqueta"), null);
        var sku = Sku.Create(product.Id, "JKT-S", Price.From(10m), null, true);
        product.AddSku(sku);

        var repo = new Mock<IProductRepository>();
        repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var guard = new Mock<ISkuLifecycleGuard>();
        guard.Setup(x => x.GetProtectionAsync(sku.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SkuProtectionStatus(true, false, false));

        var handler = new DeleteSkuCommandHandler(repo.Object, Mock.Of<ICatalogUnitOfWork>(), guard.Object);
        var act = () => handler.Handle(new DeleteSkuCommand(product.Id, sku.Id), CancellationToken.None);

        await act.Should().ThrowAsync<CatalogConflictException>()
            .Where(e => e.ErrorCode == CatalogErrorCodes.SkuDeleteProtected);
    }

    [Fact]
    public async Task UpdateSku_CodeChangeWhenProtected_ThrowsConflict()
    {
        var product = Product.CreateWithSkus("Jaqueta", Slug.From("jaqueta"), null);
        var sku = Sku.Create(product.Id, "OLD", Price.From(10m), null, true);
        product.AddSku(sku);

        var repo = new Mock<IProductRepository>();
        repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var guard = new Mock<ISkuLifecycleGuard>();
        guard.Setup(x => x.GetProtectionAsync(sku.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SkuProtectionStatus(false, true, false));

        var handler = new UpdateSkuCommandHandler(
            repo.Object, Mock.Of<ICatalogUnitOfWork>(), EmptyLookup().Object, guard.Object);

        var act = () => handler.Handle(
            new UpdateSkuCommand(product.Id, sku.Id, "NEW", 10m, null, [], true),
            CancellationToken.None);

        await act.Should().ThrowAsync<CatalogConflictException>()
            .Where(e => e.ErrorCode == CatalogErrorCodes.SkuCodeChangeProtected);
    }

    private static Mock<IAttributeDefinitionLookup> EmptyLookup()
    {
        var lookup = new Mock<IAttributeDefinitionLookup>();
        lookup.Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, AttributeDefinitionSnapshot>());
        return lookup;
    }

    private static Mock<IAttributeDefinitionLookup> LookupWithCustom(Guid defId, string name)
    {
        var snapshot = new AttributeDefinitionSnapshot(
            defId,
            name,
            AllowCustomValues: true,
            new Dictionary<Guid, AttributeValueSnapshot>());

        var lookup = new Mock<IAttributeDefinitionLookup>();
        lookup.Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, AttributeDefinitionSnapshot> { [defId] = snapshot });
        return lookup;
    }
}

public sealed class ValidatorTests
{
    [Fact]
    public void AddSkuValidator_RegularPriceZero_Fails()
    {
        var result = new AddSkuValidator().Validate(
            new AddSkuCommand(Guid.NewGuid(), "A", 0m, null, [], true));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "regularPrice" || e.PropertyName == "RegularPrice");
    }

    [Fact]
    public void AddSkuValidator_PromoGreaterOrEqualRegular_Fails()
    {
        var equal = new AddSkuValidator().Validate(
            new AddSkuCommand(Guid.NewGuid(), "A", 10m, 10m, [], true));
        equal.IsValid.Should().BeFalse();

        var greater = new AddSkuValidator().Validate(
            new AddSkuCommand(Guid.NewGuid(), "A", 10m, 11m, [], true));
        greater.IsValid.Should().BeFalse();
    }

    [Fact]
    public void AddSkuValidator_PromoLessThanRegular_Passes()
    {
        var result = new AddSkuValidator().Validate(
            new AddSkuCommand(Guid.NewGuid(), "A", 10m, 9.99m, [], true));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void AddSkuValidator_MoreThanTwoDecimals_Fails()
    {
        var result = new AddSkuValidator().Validate(
            new AddSkuCommand(Guid.NewGuid(), "A", 10.999m, null, [], true));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void SkuAttribute_PredefinedValid_Passes()
    {
        var result = new SkuAttributeCreateDtoValidator().Validate(
            new SkuAttributeCreateDto(Guid.NewGuid(), Guid.NewGuid(), null, null));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void SkuAttribute_CustomNameValid_Passes()
    {
        var result = new SkuAttributeCreateDtoValidator().Validate(
            new SkuAttributeCreateDto(Guid.NewGuid(), null, "Variadas", null));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void SkuAttribute_CustomAndValueIdTogether_Fails()
    {
        var result = new SkuAttributeCreateDtoValidator().Validate(
            new SkuAttributeCreateDto(Guid.NewGuid(), Guid.NewGuid(), "X", null));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void SkuAttribute_CustomNameEmpty_Fails()
    {
        var result = new SkuAttributeCreateDtoValidator().Validate(
            new SkuAttributeCreateDto(Guid.NewGuid(), null, "  ", null));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void SkuAttribute_MissingDefinitionId_Fails()
    {
        var result = new SkuAttributeCreateDtoValidator().Validate(
            new SkuAttributeCreateDto(null, null, "X", null));
        result.IsValid.Should().BeFalse();
    }
}

public sealed class SkuAttributeFactoryTests
{
    [Fact]
    public async Task CreateFromDtos_InvalidValueId_Fails()
    {
        var defId = Guid.NewGuid();
        var snapshot = new AttributeDefinitionSnapshot(
            defId, "Cor", false,
            new Dictionary<Guid, AttributeValueSnapshot>
            {
                [Guid.NewGuid()] = new(Guid.NewGuid(), "Rosa")
            });

        var lookup = new Mock<IAttributeDefinitionLookup>();
        lookup.Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, AttributeDefinitionSnapshot> { [defId] = snapshot });

        var act = () => SkuAttributeFactory.CreateFromDtosAsync(
            [new SkuAttributeCreateDto(defId, Guid.NewGuid(), null, null)],
            lookup.Object,
            "attributes",
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task CreateFromDtos_ValueFromOtherDefinition_Fails()
    {
        var defA = Guid.NewGuid();
        var defB = Guid.NewGuid();
        var valueOnB = Guid.NewGuid();

        var lookup = new Mock<IAttributeDefinitionLookup>();
        lookup.Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, AttributeDefinitionSnapshot>
            {
                [defA] = new(defA, "Cor", false, new Dictionary<Guid, AttributeValueSnapshot>()),
                [defB] = new(defB, "Tamanho", false, new Dictionary<Guid, AttributeValueSnapshot>
                {
                    [valueOnB] = new(valueOnB, "M")
                })
            });

        var act = () => SkuAttributeFactory.CreateFromDtosAsync(
            [new SkuAttributeCreateDto(defA, valueOnB, null, null)],
            lookup.Object,
            "attributes",
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }
}

public sealed class SkuCodeGeneratorTests
{
    [Fact]
    public void GenerateUnique_CollisionAppendsSuffix()
    {
        var existing = new HashSet<string>(StringComparer.Ordinal) { "CONJUNTO-FLORES-ROSA-M" };
        var code = SkuCodeGenerator.GenerateUnique(
            "Conjunto Flores",
            ["Rosa", "M"],
            existing);

        code.Should().Be("CONJUNTO-FLORES-ROSA-M-2");
    }

    [Fact]
    public void Normalize_UppercasesAndSlugifies()
    {
        SkuCodeGenerator.Normalize("  azul claro  ").Should().Be("AZUL-CLARO");
    }
}

public sealed class ProductImageDomainTests
{
    [Fact]
    public void AddImage_BeyondMax_Throws()
    {
        var product = Product.CreateWithSkus("P", Slug.From("p"), null);
        for (var i = 0; i < Product.MaxImages; i++)
            product.AddImage(ProductImage.Create(product.Id, $"/u/{i}", null, i, false));

        var act = () => product.AddImage(ProductImage.Create(product.Id, "/u/x", null, 99, false));
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RemovePrimary_PromotesNext()
    {
        var product = Product.CreateWithSkus("P", Slug.From("p"), null);
        var a = ProductImage.Create(product.Id, "/a", null, 0, true);
        var b = ProductImage.Create(product.Id, "/b", null, 1, false);
        product.AddImage(a);
        product.AddImage(b);

        product.RemoveImage(a.Id);

        product.Images.Should().ContainSingle();
        product.Images.Single().IsPrimary.Should().BeTrue();
        product.Images.Single().Id.Should().Be(b.Id);
    }

    [Fact]
    public void SetPrimary_SwitchesPrimaryFlag()
    {
        var product = Product.CreateWithSkus("P", Slug.From("p"), null);
        var a = ProductImage.Create(product.Id, "/a", null, 0, true);
        var b = ProductImage.Create(product.Id, "/b", null, 1, false);
        product.AddImage(a);
        product.AddImage(b);

        product.PromoteImageToPrimary(b.Id);

        a.IsPrimary.Should().BeFalse();
        b.IsPrimary.Should().BeTrue();
    }
}
