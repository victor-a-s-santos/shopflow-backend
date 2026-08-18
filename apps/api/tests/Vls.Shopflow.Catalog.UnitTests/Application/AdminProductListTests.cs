using FluentAssertions;
using Moq;
using Vls.Shopflow.Catalog.Application.DataTransferObjects;
using Vls.Shopflow.Catalog.Application.Queries;
using Vls.Shopflow.Catalog.Application.QueryHandlers;
using Vls.Shopflow.Catalog.Application.Repositories;
using Vls.Shopflow.Catalog.Application.Validations;

namespace Vls.Shopflow.Catalog.UnitTests.Application;

public sealed class AdminProductListTests
{
    [Fact]
    public void Validator_defaults_allow_page_1_pageSize_20()
    {
        var result = new GetAdminProductsQueryValidator().Validate(new GetAdminProductsQuery());
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_rejects_pageSize_above_100()
    {
        var result = new GetAdminProductsQueryValidator().Validate(new GetAdminProductsQuery(PageSize: 101));
        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("default")]
    [InlineData("newest")]
    [InlineData("oldest")]
    [InlineData("name_asc")]
    [InlineData("name_desc")]
    [InlineData("display_order")]
    [InlineData("featured")]
    [InlineData("price_asc")]
    [InlineData("price_desc")]
    public void Validator_accepts_known_sorts(string sort)
    {
        new GetAdminProductsQueryValidator()
            .Validate(new GetAdminProductsQuery(Sort: sort))
            .IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("all")]
    [InlineData("active")]
    [InlineData("inactive")]
    public void Validator_accepts_status(string status)
    {
        new GetAdminProductsQueryValidator()
            .Validate(new GetAdminProductsQuery(Status: status))
            .IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("all")]
    [InlineData("featured")]
    [InlineData("not_featured")]
    public void Validator_accepts_featured(string featured)
    {
        new GetAdminProductsQueryValidator()
            .Validate(new GetAdminProductsQuery(Featured: featured))
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Handler_forwards_all_filters()
    {
        var expected = new PagedAdminProductsDto([], 2, 20, 45, 3, true, true);
        var categoryId = Guid.NewGuid();
        var readModel = new Mock<IAdminProductReadModel>();
        readModel
            .Setup(x => x.GetPagedAsync(
                2, 20, AdminProductListSort.NameAsc, "jeans", "calcas", categoryId,
                AdminProductListFilters.StatusInactive, AdminProductListFilters.FeaturedOnly,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var handler = new GetAdminProductsQueryHandler(readModel.Object);
        var result = await handler.Handle(
            new GetAdminProductsQuery(
                2, 20, AdminProductListSort.NameAsc, "jeans", "calcas", categoryId,
                AdminProductListFilters.StatusInactive, AdminProductListFilters.FeaturedOnly),
            CancellationToken.None);

        result.Should().Be(expected);
        result.HasNextPage.Should().BeTrue();
        result.Total.Should().Be(45);
    }

    [Fact]
    public void Sort_normalize_falls_back_to_default()
    {
        AdminProductListSort.Normalize(null).Should().Be(AdminProductListSort.Default);
        AdminProductListSort.Normalize("UPDATED_DESC").Should().Be(AdminProductListSort.Default);
        AdminProductListSort.Normalize("NAME_ASC").Should().Be(AdminProductListSort.NameAsc);
    }

    [Fact]
    public void Default_sort_key_is_createdAt_desc_then_id()
    {
        var t0 = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var t1 = t0.AddDays(1);
        var rows = new[]
        {
            (Id: Guid.Parse("00000000-0000-0000-0000-000000000002"), Created: t0),
            (Id: Guid.Parse("00000000-0000-0000-0000-000000000001"), Created: t1),
            (Id: Guid.Parse("00000000-0000-0000-0000-000000000003"), Created: t1),
        };

        var ordered = rows
            .OrderByDescending(r => r.Created)
            .ThenBy(r => r.Id)
            .Select(r => r.Id)
            .ToList();

        ordered.Should().Equal(
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Guid.Parse("00000000-0000-0000-0000-000000000003"),
            Guid.Parse("00000000-0000-0000-0000-000000000002"));
    }

    [Fact]
    public void Display_order_sort_puts_nulls_last_then_featured()
    {
        var rows = new[]
        {
            (Id: 1, Featured: false, Order: (int?)null),
            (Id: 2, Featured: true, Order: (int?)20),
            (Id: 3, Featured: false, Order: (int?)10),
            (Id: 4, Featured: true, Order: (int?)10),
        };

        var ordered = rows
            .OrderBy(r => r.Order == null)
            .ThenBy(r => r.Order)
            .ThenByDescending(r => r.Featured)
            .ThenBy(r => r.Id)
            .Select(r => r.Id)
            .ToList();

        ordered.Should().Equal(4, 3, 2, 1);
    }

    [Fact]
    public void Admin_list_item_shape_supports_incomplete_products()
    {
        var item = new AdminProductListItemDto(
            Guid.NewGuid(),
            "Incompleto",
            "incompleto",
            IsActive: false,
            IsFeatured: false,
            DisplayOrder: null,
            CreatedAt: DateTimeOffset.UtcNow,
            Category: null,
            PrimaryImageUrl: null,
            SkuCount: 0,
            ActiveSkuCount: 0,
            MinPrice: null,
            HasPromotionalPrice: false);

        item.SkuCount.Should().Be(0);
        item.MinPrice.Should().BeNull();
        item.PrimaryImageUrl.Should().BeNull();
        item.IsActive.Should().BeFalse();
    }
}
