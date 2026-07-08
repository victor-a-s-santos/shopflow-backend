using Vls.Shopflow.BuildingBlocks.Application.Interfaces;

namespace Vls.Shopflow.Inventory.Application.Commands;

public sealed record ReserveStockCommand(
    Guid SkuId,
    int Quantity,
    DateTimeOffset? ExpiresAt) : ICommand<Guid>;
