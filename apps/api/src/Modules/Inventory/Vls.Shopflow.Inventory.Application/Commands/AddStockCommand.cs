using Vls.Shopflow.BuildingBlocks.Application.Interfaces;

namespace Vls.Shopflow.Inventory.Application.Commands;

public sealed record AddStockCommand(
    Guid SkuId,
    int Quantity,
    string? Reason) : ICommand;
