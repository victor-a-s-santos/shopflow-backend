using Vls.Shopflow.BuildingBlocks.Application.Interfaces;
using Vls.Shopflow.Inventory.Application.DataTransferObjects;

namespace Vls.Shopflow.Inventory.Application.Commands;

public sealed record RemoveStockCommand(
    Guid SkuId,
    int Quantity,
    string? Reason) : ICommand<InventoryItemDto>;
