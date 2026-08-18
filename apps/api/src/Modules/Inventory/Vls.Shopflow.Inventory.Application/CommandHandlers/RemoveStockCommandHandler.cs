using MediatR;
using Microsoft.Extensions.Logging;
using Vls.Shopflow.Inventory.Application.Commands;
using Vls.Shopflow.Inventory.Application.DataTransferObjects;
using Vls.Shopflow.Inventory.Application.Interfaces;
using Vls.Shopflow.Inventory.Application.Repositories;
using Vls.Shopflow.Inventory.Domain.Exceptions;

namespace Vls.Shopflow.Inventory.Application.CommandHandlers;

public sealed class RemoveStockCommandHandler(
    ISkuExistenceChecker skuChecker,
    IInventoryAtomicOperations atomicOperations,
    IInventoryReadModel readModel,
    ILogger<RemoveStockCommandHandler> logger)
    : IRequestHandler<RemoveStockCommand, InventoryItemDto>
{
    public async Task<InventoryItemDto> Handle(RemoveStockCommand cmd, CancellationToken cancellationToken)
    {
        if (!await skuChecker.ExistsAsync(cmd.SkuId, cancellationToken))
            throw new SkuNotFoundException(cmd.SkuId);

        await atomicOperations.RemoveStockAsync(cmd.SkuId, cmd.Quantity, cmd.Reason, cancellationToken);

        var updated = await readModel.GetBySkuIdAsync(cmd.SkuId, cancellationToken)
                      ?? throw new InventoryItemNotFoundException(cmd.SkuId);

        logger.LogInformation(
            "Removed {Quantity} units from SKU {SkuId}. Remaining available={Available}",
            cmd.Quantity, cmd.SkuId, updated.AvailableQuantity);

        return updated;
    }
}
