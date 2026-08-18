using FluentAssertions;
using Moq;
using Vls.Shopflow.Catalog.Application.DataTransferObjects;
using Vls.Shopflow.Catalog.Application.Queries;
using Vls.Shopflow.Catalog.Application.QueryHandlers;
using Vls.Shopflow.Catalog.Application.Repositories;
using Vls.Shopflow.Catalog.Application.Validations;
using Vls.Shopflow.Catalog.Domain.Entities;

namespace Vls.Shopflow.Catalog.UnitTests.Application;

public sealed class ProductCategoryFilterTests
{
    [Fact]
    public async Task GetProductsHandler_forwards_categorySlug_categoryId_and_q()
    {
        var expected = new PagedProductsDto([], 1, 16, 0, 0, false, false);
        var categoryId = Guid.NewGuid();
        var readModel = new Mock<IProductReadModel>();
        readModel
            .Setup(x => x.GetPagedAsync(
                1, 16, ProductListSort.Default, "calcas", categoryId, "jeans", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var handler = new GetProductsQueryHandler(readModel.Object);
        var result = await handler.Handle(
            new GetProductsQuery(1, 16, ProductListSort.Default, "calcas", categoryId, "jeans"),
            CancellationToken.None);

        result.Should().Be(expected);
        readModel.Verify(x => x.GetPagedAsync(
            1, 16, ProductListSort.Default, "calcas", categoryId, "jeans", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Validator_accepts_categorySlug_and_q()
    {
        var result = new GetProductsQueryValidator().Validate(
            new GetProductsQuery(1, 16, ProductListSort.Default, "calcas", null, "jeans"));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_rejects_empty_categoryId()
    {
        var result = new GetProductsQueryValidator().Validate(
            new GetProductsQuery(1, 16, CategoryId: Guid.Empty));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Category_constructor_generates_slug_from_name()
    {
        var category = new Category("Calças");
        category.Slug.Value.Should().Be("calcas");
    }

    [Fact]
    public void Category_slug_matches_filter_examples()
    {
        new Category("Calças").Slug.Value.Should().Be("calcas");
        new Category("Tops / Regatas").Slug.Value.Should().Be("tops-regatas");
        new Category("Moda Praia").Slug.Value.Should().Be("moda-praia");
    }

    [Fact]
    public void Unknown_categorySlug_contract_is_empty_page()
    {
        // Documented API decision: unknown slug → empty paged result (not 404).
        var empty = new PagedProductsDto([], 1, 16, 0, 0, false, false);
        empty.TotalItems.Should().Be(0);
        empty.HasNextPage.Should().BeFalse();
        empty.Items.Should().BeEmpty();
    }
}
