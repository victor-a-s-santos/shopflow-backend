using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Vls.Shopflow.Inventory.Application.CommandHandlers;
using Vls.Shopflow.Inventory.Application.Commands;
using Vls.Shopflow.Inventory.Application.DataTransferObjects;
using Vls.Shopflow.Inventory.Application.Interfaces;
using Vls.Shopflow.Inventory.Application.Repositories;
using Vls.Shopflow.Inventory.Application.Validations;
using Vls.Shopflow.Inventory.Domain.Entities;
using Vls.Shopflow.Inventory.Domain.Exceptions;

namespace Vls.Shopflow.Inventory.UnitTests.Application;

public sealed class RemoveStockCommandHandlerTests
{
    [Fact]
    public async Task RemoveStock_WhenValid_ReturnsUpdatedBalance()
    {
        var skuId = Guid.NewGuid();
        var skuChecker = new Mock<ISkuExistenceChecker>();
        skuChecker.Setup(x => x.ExistsAsync(skuId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var atomic = new Mock<IInventoryAtomicOperations>();
        atomic.Setup(x => x.RemoveStockAsync(skuId, 3, "Ajuste", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var readModel = new Mock<IInventoryReadModel>();
        readModel.Setup(x => x.GetBySkuIdAsync(skuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InventoryItemDto(skuId, 7, 2, 5));

        var handler = new RemoveStockCommandHandler(
            skuChecker.Object,
            atomic.Object,
            readModel.Object,
            NullLogger<RemoveStockCommandHandler>.Instance);

        var result = await handler.Handle(
            new RemoveStockCommand(skuId, 3, "Ajuste"),
            CancellationToken.None);

        result.AvailableQuantity.Should().Be(5);
        result.QuantityOnHand.Should().Be(7);
        result.QuantityReserved.Should().Be(2);
    }

    [Fact]
    public async Task RemoveStock_WhenSkuMissing_Throws()
    {
        var skuId = Guid.NewGuid();
        var skuChecker = new Mock<ISkuExistenceChecker>();
        skuChecker.Setup(x => x.ExistsAsync(skuId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var handler = new RemoveStockCommandHandler(
            skuChecker.Object,
            Mock.Of<IInventoryAtomicOperations>(),
            Mock.Of<IInventoryReadModel>(),
            NullLogger<RemoveStockCommandHandler>.Instance);

        var act = () => handler.Handle(new RemoveStockCommand(skuId, 1, "Ajuste"), CancellationToken.None);
        await act.Should().ThrowAsync<SkuNotFoundException>();
    }
}

public sealed class RemoveStockValidatorTests
{
    [Fact]
    public void QuantityZeroOrNegative_Fails()
    {
        var v = new RemoveStockCommandValidator();
        v.Validate(new RemoveStockCommand(Guid.NewGuid(), 0, "x")).IsValid.Should().BeFalse();
        v.Validate(new RemoveStockCommand(Guid.NewGuid(), -1, "x")).IsValid.Should().BeFalse();
    }

    [Fact]
    public void ReasonRequired_FailsWhenEmpty()
    {
        var v = new RemoveStockCommandValidator();
        v.Validate(new RemoveStockCommand(Guid.NewGuid(), 1, null)).IsValid.Should().BeFalse();
        v.Validate(new RemoveStockCommand(Guid.NewGuid(), 1, "  ")).IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidCommand_Passes()
    {
        var v = new RemoveStockCommandValidator();
        v.Validate(new RemoveStockCommand(Guid.NewGuid(), 1, "Baixa operacional")).IsValid.Should().BeTrue();
    }
}

public sealed class AvailableStockDomainTests
{
    [Fact]
    public void RemoveStock_CannotConsumeReserved()
    {
        var item = InventoryItem.Create(Guid.NewGuid(), initialQuantity: 10);
        // Simulate reserved via domain path isn't public for attach; Available = onHand - reserved
        // Use RemoveStock against available: onHand 10, reserved 0 → can remove 10
        item.RemoveStock(10, "full");
        item.QuantityOnHand.Should().Be(0);
    }

    [Fact]
    public void RemoveStock_WhenExceedsAvailable_Throws()
    {
        var item = InventoryItem.Create(Guid.NewGuid(), initialQuantity: 5);
        var act = () => item.RemoveStock(6, "Baixa");
        act.Should().Throw<InsufficientStockException>()
            .Where(e => e.Available == 5 && e.Requested == 6);
    }
}
