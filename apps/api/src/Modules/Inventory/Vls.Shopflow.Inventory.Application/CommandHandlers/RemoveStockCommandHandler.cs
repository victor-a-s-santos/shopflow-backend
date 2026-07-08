using MediatR;
using Microsoft.Extensions.Logging;
using Vls.Shopflow.Inventory.Application.Commands;
using Vls.Shopflow.Inventory.Application.Interfaces;
using Vls.Shopflow.Inventory.Application.Repositories;
using Vls.Shopflow.Inventory.Domain.Exceptions;

namespace Vls.Shopflow.Inventory.Application.CommandHandlers;

public sealed class RemoveStockCommandHandler(
    ISkuExistenceChecker skuChecker,
    IInventoryAtomicOperations atomicOperations,
    ILogger<RemoveStockCommandHandler> logger)
    : IRequestHandler<RemoveStockCommand>
{
    public async Task Handle(RemoveStockCommand cmd, CancellationToken cancellationToken)
    {
        if (!await skuChecker.ExistsAsync(cmd.SkuId, cancellationToken))
            throw new SkuNotFoundException(cmd.SkuId);

        await atomicOperations.RemoveStockAsync(cmd.SkuId, cmd.Quantity, cmd.Reason, cancellationToken);

        logger.LogInformation(
            "Removed {Quantity} units from SKU {SkuId}",
            cmd.Quantity, cmd.SkuId);
    }
}
