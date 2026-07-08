using Vls.Shopflow.BuildingBlocks.Application.Interfaces;

namespace Vls.Shopflow.Inventory.Application.Commands;

public sealed record CreateInventoryItemCommand(
    Guid SkuId,
    int InitialQuantity = 0) : ICommand<Guid>;
