using FluentAssertions;
using FluentValidation.TestHelper;
using Moq;
using Vls.Shopflow.Inventory.Application.DataTransferObjects;
using Vls.Shopflow.Inventory.Application.Queries;
using Vls.Shopflow.Inventory.Application.QueryHandlers;
using Vls.Shopflow.Inventory.Application.Repositories;
using Vls.Shopflow.Inventory.Application.Services;
using Vls.Shopflow.Inventory.Application.Validations;

namespace Vls.Shopflow.Inventory.UnitTests.Application;

public sealed class GetAdminInventorySkusQueryHandlerTests
{
    [Fact]
    public async Task Handle_DelegatesToReadModel_WithQueryArgs()
    {
        var expected = new PagedAdminInventorySkusDto([], 1, 20, 0, 0, false, false);
        var readModel = new Mock<IAdminInventorySkuReadModel>();
        var productId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        readModel.Setup(x => x.GetPagedAsync(
                2,
                10,
                AdminInventorySkuListSort.AvailableDesc,
                "jean",
                productId,
                "calcas",
                categoryId,
                AdminInventorySkuListFilters.StatusActive,
                AdminInventorySkuListFilters.StockLowStock,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var handler = new GetAdminInventorySkusQueryHandler(readModel.Object);

        var result = await handler.Handle(
            new GetAdminInventorySkusQuery(
                2,
                10,
                AdminInventorySkuListSort.AvailableDesc,
                "jean",
                productId,
                "calcas",
                categoryId,
                AdminInventorySkuListFilters.StatusActive,
                AdminInventorySkuListFilters.StockLowStock),
            CancellationToken.None);

        result.Should().BeSameAs(expected);
        readModel.VerifyAll();
    }
}

public sealed class GetAdminInventorySkusQueryValidatorTests
{
    private readonly GetAdminInventorySkusQueryValidator _validator = new();

    [Fact]
    public void Validate_Defaults_Succeed()
    {
        var result = _validator.TestValidate(new GetAdminInventorySkusQuery());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_PageSizeAbove100_Fails()
    {
        var result = _validator.TestValidate(new GetAdminInventorySkusQuery(PageSize: 101));
        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact]
    public void Validate_PageBelow1_Fails()
    {
        var result = _validator.TestValidate(new GetAdminInventorySkusQuery(Page: 0));
        result.ShouldHaveValidationErrorFor(x => x.Page);
    }

    [Fact]
    public void Validate_InvalidSort_Fails()
    {
        var result = _validator.TestValidate(new GetAdminInventorySkusQuery(Sort: "updated_at_desc"));
        result.ShouldHaveValidationErrorFor(x => x.Sort);
    }

    [Fact]
    public void Validate_InvalidStatus_Fails()
    {
        var result = _validator.TestValidate(new GetAdminInventorySkusQuery(Status: "draft"));
        result.ShouldHaveValidationErrorFor(x => x.Status);
    }

    [Fact]
    public void Validate_InvalidStockStatus_Fails()
    {
        var result = _validator.TestValidate(new GetAdminInventorySkusQuery(StockStatus: "critical"));
        result.ShouldHaveValidationErrorFor(x => x.StockStatus);
    }

    [Theory]
    [InlineData(AdminInventorySkuListSort.Default)]
    [InlineData(AdminInventorySkuListSort.ProductNameAsc)]
    [InlineData(AdminInventorySkuListSort.AvailableAsc)]
    [InlineData(AdminInventorySkuListSort.PriceDesc)]
    public void Validate_AllowedSorts_Succeed(string sort)
    {
        var result = _validator.TestValidate(new GetAdminInventorySkusQuery(Sort: sort));
        result.ShouldNotHaveValidationErrorFor(x => x.Sort);
    }
}

public sealed class AdminInventoryStockStatusTests
{
    [Theory]
    [InlineData(8, 0, "in_stock")]
    [InlineData(5, 0, "low_stock")]
    [InlineData(1, 2, "low_stock")]
    [InlineData(0, 0, "out_of_stock")]
    [InlineData(0, 3, "reserved")]
    public void Compute_MapsQuantitiesToStatus(int available, int reserved, string expected)
    {
        AdminInventoryStockStatus.Compute(available, reserved).Should().Be(expected);
    }
}
