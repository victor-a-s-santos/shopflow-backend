using FluentAssertions;
using FluentValidation.TestHelper;
using Moq;
using Vls.Shopflow.Inventory.Application.DataTransferObjects;
using Vls.Shopflow.Inventory.Application.Queries;
using Vls.Shopflow.Inventory.Application.QueryHandlers;
using Vls.Shopflow.Inventory.Application.Repositories;
using Vls.Shopflow.Inventory.Application.Validations;

namespace Vls.Shopflow.Inventory.UnitTests.Application;

public sealed class GetSkuAvailabilityBatchQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsAvailabilityInRequestOrder_IncludingMissingAndDuplicates()
    {
        var skuA = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var skuB = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var skuMissing = Guid.Parse("33333333-3333-3333-3333-333333333333");

        var readModel = new Mock<IInventoryReadModel>();
        readModel.Setup(x => x.GetBySkuIdsAsync(
                It.IsAny<IReadOnlyList<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new InventoryItemDto(skuA, QuantityOnHand: 25, QuantityReserved: 5, AvailableQuantity: 20),
                new InventoryItemDto(skuB, QuantityOnHand: 3, QuantityReserved: 0, AvailableQuantity: 3)
            });

        var handler = new GetSkuAvailabilityBatchQueryHandler(readModel.Object);

        var result = await handler.Handle(
            new GetSkuAvailabilityBatchQuery([skuA, skuMissing, skuB, skuA]),
            CancellationToken.None);

        result.Items.Should().HaveCount(4);

        result.Items[0].Should().BeEquivalentTo(new SkuAvailabilityBatchItemDto(
            skuA, 20, 25, 5, true));
        result.Items[1].Should().BeEquivalentTo(new SkuAvailabilityBatchItemDto(
            skuMissing, null, null, null, false));
        result.Items[2].Should().BeEquivalentTo(new SkuAvailabilityBatchItemDto(
            skuB, 3, 3, 0, true));
        result.Items[3].SkuId.Should().Be(skuA);
        result.Items[3].Exists.Should().BeTrue();
        result.Items[3].AvailableQuantity.Should().Be(20);

        readModel.Verify(
            x => x.GetBySkuIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_DoesNotCallWriteOperations()
    {
        var skuId = Guid.NewGuid();
        var readModel = new Mock<IInventoryReadModel>(MockBehavior.Strict);
        readModel.Setup(x => x.GetBySkuIdsAsync(
                It.IsAny<IReadOnlyList<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<InventoryItemDto>());

        var handler = new GetSkuAvailabilityBatchQueryHandler(readModel.Object);

        await handler.Handle(new GetSkuAvailabilityBatchQuery([skuId]), CancellationToken.None);

        readModel.VerifyAll();
    }
}

public sealed class GetSkuAvailabilityBatchQueryValidatorTests
{
    private readonly GetSkuAvailabilityBatchQueryValidator _validator = new();

    [Fact]
    public void Validate_EmptyList_Fails()
    {
        var result = _validator.TestValidate(new GetSkuAvailabilityBatchQuery([]));
        result.ShouldHaveValidationErrorFor(x => x.SkuIds);
    }

    [Fact]
    public void Validate_OverLimit_Fails()
    {
        var ids = Enumerable.Range(0, GetSkuAvailabilityBatchQueryValidator.MaxSkuIds + 1)
            .Select(_ => Guid.NewGuid())
            .ToList();

        var result = _validator.TestValidate(new GetSkuAvailabilityBatchQuery(ids));
        result.ShouldHaveValidationErrorFor(x => x.SkuIds);
    }

    [Fact]
    public void Validate_EmptyGuid_Fails()
    {
        var result = _validator.TestValidate(new GetSkuAvailabilityBatchQuery([Guid.Empty]));
        result.ShouldHaveValidationErrorFor("SkuIds[0]");
    }

    [Fact]
    public void Validate_ValidPayload_Passes()
    {
        var result = _validator.TestValidate(
            new GetSkuAvailabilityBatchQuery([Guid.NewGuid(), Guid.NewGuid()]));
        result.ShouldNotHaveAnyValidationErrors();
    }
}
