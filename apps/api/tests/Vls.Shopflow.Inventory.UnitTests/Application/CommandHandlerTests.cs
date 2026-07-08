using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Vls.Shopflow.Inventory.Application.CommandHandlers;
using Vls.Shopflow.Inventory.Application.Commands;
using Vls.Shopflow.Inventory.Application.Interfaces;
using Vls.Shopflow.Inventory.Application.Repositories;
using Vls.Shopflow.Inventory.Domain.Entities;
using Vls.Shopflow.Inventory.Domain.Exceptions;

namespace Vls.Shopflow.Inventory.UnitTests.Application;

public sealed class CommandHandlerTests
{
    [Fact]
    public async Task CreateInventoryItem_WhenSkuMissing_ThrowsSkuNotFound()
    {
        var skuId = Guid.NewGuid();
        var skuChecker = new Mock<ISkuExistenceChecker>();
        skuChecker.Setup(x => x.ExistsAsync(skuId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var handler = new CreateInventoryItemCommandHandler(
            skuChecker.Object,
            Mock.Of<IInventoryItemRepository>(),
            Mock.Of<IInventoryUnitOfWork>(),
            NullLogger<CreateInventoryItemCommandHandler>.Instance);

        var act = () => handler.Handle(new CreateInventoryItemCommand(skuId, 10), CancellationToken.None);

        await act.Should().ThrowAsync<SkuNotFoundException>();
    }

    [Fact]
    public async Task CreateInventoryItem_WhenAlreadyExists_ThrowsConflict()
    {
        var skuId = Guid.NewGuid();
        var skuChecker = new Mock<ISkuExistenceChecker>();
        skuChecker.Setup(x => x.ExistsAsync(skuId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var repo = new Mock<IInventoryItemRepository>();
        repo.Setup(x => x.ExistsForSkuAsync(skuId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var handler = new CreateInventoryItemCommandHandler(
            skuChecker.Object,
            repo.Object,
            Mock.Of<IInventoryUnitOfWork>(),
            NullLogger<CreateInventoryItemCommandHandler>.Instance);

        var act = () => handler.Handle(new CreateInventoryItemCommand(skuId, 10), CancellationToken.None);

        await act.Should().ThrowAsync<InventoryItemAlreadyExistsException>();
    }

    [Fact]
    public async Task AddStock_WhenItemMissing_AutoCreatesInventory()
    {
        var skuId = Guid.NewGuid();
        InventoryItem? captured = null;

        var skuChecker = new Mock<ISkuExistenceChecker>();
        skuChecker.Setup(x => x.ExistsAsync(skuId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var repo = new Mock<IInventoryItemRepository>();
        repo.Setup(x => x.GetBySkuIdAsync(skuId, It.IsAny<CancellationToken>())).ReturnsAsync((InventoryItem?)null);
        repo.Setup(x => x.AddAsync(It.IsAny<InventoryItem>(), It.IsAny<CancellationToken>()))
            .Callback<InventoryItem, CancellationToken>((item, _) => captured = item)
            .Returns(Task.CompletedTask);

        var uow = new Mock<IInventoryUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new AddStockCommandHandler(
            skuChecker.Object,
            repo.Object,
            uow.Object,
            NullLogger<AddStockCommandHandler>.Instance);

        await handler.Handle(new AddStockCommand(skuId, 7, "Entrada"), CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.SkuId.Should().Be(skuId);
        captured.QuantityOnHand.Should().Be(7);
    }

    [Fact]
    public async Task ReserveStock_DelegatesToAtomicOperations()
    {
        var skuId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();

        var skuChecker = new Mock<ISkuExistenceChecker>();
        skuChecker.Setup(x => x.ExistsAsync(skuId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var atomic = new Mock<IInventoryAtomicOperations>();
        atomic.Setup(x => x.ReserveAsync(skuId, 3, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservationId);

        var handler = new ReserveStockCommandHandler(
            skuChecker.Object,
            atomic.Object,
            NullLogger<ReserveStockCommandHandler>.Instance);

        var result = await handler.Handle(new ReserveStockCommand(skuId, 3, null), CancellationToken.None);

        result.Should().Be(reservationId);
    }
}
