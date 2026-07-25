using FluentAssertions;
using Moq;
using Vls.Shopflow.Catalog.Application.CommandHandlers;
using Vls.Shopflow.Catalog.Application.Commands;
using Vls.Shopflow.Catalog.Application.DataTransferObjects;
using Vls.Shopflow.Catalog.Application.Interfaces;
using Vls.Shopflow.Catalog.Application.Queries;
using Vls.Shopflow.Catalog.Application.QueryHandlers;
using Vls.Shopflow.Catalog.Application.Repositories;
using Vls.Shopflow.Catalog.Application.Validations;
using Vls.Shopflow.Catalog.Domain.Entities;
using Vls.Shopflow.Catalog.Domain.ValueObjects;

namespace Vls.Shopflow.Catalog.UnitTests.Application;

public sealed class ProductListPaginationTests
{
    [Fact]
    public void Validator_defaults_allow_page_1_pageSize_16()
    {
        var result = new GetProductsQueryValidator().Validate(new GetProductsQuery());
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_rejects_pageSize_above_48()
    {
        var result = new GetProductsQueryValidator().Validate(new GetProductsQuery(1, 49));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(GetProductsQuery.PageSize));
    }

    [Fact]
    public void Validator_rejects_page_below_1()
    {
        var result = new GetProductsQueryValidator().Validate(new GetProductsQuery(0, 16));
        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("default")]
    [InlineData("newest")]
    [InlineData("price_asc")]
    [InlineData("price_desc")]
    [InlineData("name_asc")]
    public void Validator_accepts_known_sorts(string sort)
    {
        var result = new GetProductsQueryValidator().Validate(new GetProductsQuery(1, 16, sort));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_rejects_unknown_sort()
    {
        var result = new GetProductsQueryValidator().Validate(new GetProductsQuery(1, 16, "updated_at"));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ProductListSort_Normalize_falls_back_to_default()
    {
        ProductListSort.Normalize(null).Should().Be(ProductListSort.Default);
        ProductListSort.Normalize("  ").Should().Be(ProductListSort.Default);
        ProductListSort.Normalize("NEWEST").Should().Be(ProductListSort.Newest);
    }

    [Fact]
    public async Task GetProductsHandler_forwards_sort_and_pagination()
    {
        var expected = new PagedProductsDto([], 2, 16, 40, 3, true, true);
        var readModel = new Mock<IProductReadModel>();
        readModel
            .Setup(x => x.GetPagedAsync(
                2, 16, ProductListSort.Newest, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var handler = new GetProductsQueryHandler(readModel.Object);
        var result = await handler.Handle(new GetProductsQuery(2, 16, ProductListSort.Newest), CancellationToken.None);

        result.Should().Be(expected);
        result.HasNextPage.Should().BeTrue();
        result.Total.Should().Be(40);
    }

    [Fact]
    public void PagedProductsDto_exposes_hasNext_for_load_more()
    {
        var page1 = new PagedProductsDto([], 1, 16, 52, 4, true, false);
        page1.HasNextPage.Should().BeTrue();
        page1.HasPreviousPage.Should().BeFalse();
        page1.TotalPages.Should().Be(4);
        page1.TotalItems.Should().Be(52);

        var last = new PagedProductsDto([], 4, 16, 52, 4, false, true);
        last.HasNextPage.Should().BeFalse();
    }
}

public sealed class ProductDisplaySettingsTests
{
    [Fact]
    public void CreateWithSkus_defaults_display_fields()
    {
        var product = Product.CreateWithSkus("Camiseta", Slug.From("camiseta"), null);
        product.IsFeatured.Should().BeFalse();
        product.DisplayOrder.Should().BeNull();
        product.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ChangeDisplaySettings_sets_featured_and_order()
    {
        var product = Product.CreateWithSkus("Camiseta", Slug.From("camiseta"), null);
        product.ChangeDisplaySettings(true, 10);
        product.IsFeatured.Should().BeTrue();
        product.DisplayOrder.Should().Be(10);
    }

    [Fact]
    public void ChangeDisplaySettings_rejects_negative_order()
    {
        var product = Product.CreateWithSkus("Camiseta", Slug.From("camiseta"), null);
        var act = () => product.ChangeDisplaySettings(false, -1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void UpdateProductValidator_rejects_negative_displayOrder()
    {
        var result = new UpdateProductCommandValidator().Validate(
            new UpdateProductCommand(Guid.NewGuid(), "A", null, null, true, false, -1, UpdateDisplaySettings: true));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateProductHandler_persists_display_settings_when_flagged()
    {
        var product = Product.CreateWithSkus("Camiseta", Slug.From("camiseta"), null);
        var repo = new Mock<IProductRepository>();
        repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var slugService = new Mock<ISlugService>();
        var uow = new Mock<ICatalogUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new UpdateProductCommandHandler(repo.Object, slugService.Object, uow.Object);
        await handler.Handle(
            new UpdateProductCommand(product.Id, "Camiseta", null, null, true, true, 5, UpdateDisplaySettings: true),
            CancellationToken.None);

        product.IsFeatured.Should().BeTrue();
        product.DisplayOrder.Should().Be(5);
    }

    [Fact]
    public async Task UpdateProductHandler_preserves_display_when_not_flagged()
    {
        var product = Product.CreateWithSkus("Camiseta", Slug.From("camiseta"), null, isFeatured: true, displayOrder: 3);
        var repo = new Mock<IProductRepository>();
        repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        var uow = new Mock<ICatalogUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new UpdateProductCommandHandler(repo.Object, Mock.Of<ISlugService>(), uow.Object);
        await handler.Handle(
            new UpdateProductCommand(product.Id, "Camiseta", null, null, true),
            CancellationToken.None);

        product.IsFeatured.Should().BeTrue();
        product.DisplayOrder.Should().Be(3);
    }

    [Fact]
    public void Default_sort_key_orders_featured_then_displayOrder_then_createdAt_then_id()
    {
        var t0 = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var t1 = t0.AddDays(1);

        var rows = new[]
        {
            (Id: Guid.Parse("00000000-0000-0000-0000-000000000004"), Featured: false, Order: (int?)10, Created: t1),
            (Id: Guid.Parse("00000000-0000-0000-0000-000000000001"), Featured: true, Order: (int?)20, Created: t0),
            (Id: Guid.Parse("00000000-0000-0000-0000-000000000002"), Featured: true, Order: (int?)10, Created: t0),
            (Id: Guid.Parse("00000000-0000-0000-0000-000000000003"), Featured: false, Order: (int?)null, Created: t1),
            (Id: Guid.Parse("00000000-0000-0000-0000-000000000005"), Featured: false, Order: (int?)10, Created: t0),
        };

        var ordered = rows
            .OrderByDescending(r => r.Featured)
            .ThenBy(r => r.Order == null)
            .ThenBy(r => r.Order)
            .ThenByDescending(r => r.Created)
            .ThenBy(r => r.Id)
            .Select(r => r.Id)
            .ToList();

        ordered.Should().Equal(
            Guid.Parse("00000000-0000-0000-0000-000000000002"), // featured, order 10
            Guid.Parse("00000000-0000-0000-0000-000000000001"), // featured, order 20
            Guid.Parse("00000000-0000-0000-0000-000000000004"), // not featured, order 10, newer
            Guid.Parse("00000000-0000-0000-0000-000000000005"), // not featured, order 10, older
            Guid.Parse("00000000-0000-0000-0000-000000000003")  // null order last
        );
    }
}
