using FluentAssertions;
using Moq;
using Vls.Shopflow.BuildingBlocks.Domain.ValueObjects;
using Vls.Shopflow.Catalog.Application.CommandHandlers;
using Vls.Shopflow.Catalog.Application.Commands;
using Vls.Shopflow.Catalog.Application.DataTransferObjects;
using Vls.Shopflow.Catalog.Application.QueryHandlers;
using Vls.Shopflow.Catalog.Application.Queries;
using Vls.Shopflow.Catalog.Application.Repositories;
using Vls.Shopflow.Catalog.Domain.Entities;
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

        var handler = new AddSkuCommandHandler(repo.Object, uow.Object);
        var skuId = await handler.Handle(
            new AddSkuCommand(product.Id, "JKT-M", 199.90m, null, [], true),
            CancellationToken.None);

        skuId.Should().NotBeEmpty();
        product.Skus.Should().ContainSingle(s => s.Id == skuId && s.Code == "JKT-M");
        uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateSku_ChangesExistingSku()
    {
        var product = Product.CreateWithSkus("Jaqueta", Slug.From("jaqueta"), null);
        var sku = Sku.Create(product.Id, "JKT-S", Price.From(199.90m), null, true);
        product.AddSku(sku);

        var repo = new Mock<IProductRepository>();
        repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var uow = new Mock<ICatalogUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new UpdateSkuCommandHandler(repo.Object, uow.Object);
        await handler.Handle(
            new UpdateSkuCommand(
                product.Id,
                sku.Id,
                "JKT-M",
                249.90m,
                199.90m,
                [new SkuAttributeCreateDto(null, null, "Tamanho", "M")],
                true),
            CancellationToken.None);

        sku.Code.Should().Be("JKT-M");
        sku.Price.Regular.Amount.Should().Be(249.90m);
        sku.Attributes.Should().ContainSingle(a => a.CustomName == "Tamanho");
    }
}
