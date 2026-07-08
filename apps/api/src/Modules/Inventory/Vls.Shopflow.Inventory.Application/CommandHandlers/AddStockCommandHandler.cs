using MediatR;
using Microsoft.Extensions.Logging;
using Vls.Shopflow.Inventory.Application.Commands;
using Vls.Shopflow.Inventory.Application.Interfaces;
using Vls.Shopflow.Inventory.Application.Repositories;
using Vls.Shopflow.Inventory.Domain.Entities;
using Vls.Shopflow.Inventory.Domain.Exceptions;

namespace Vls.Shopflow.Inventory.Application.CommandHandlers;

public sealed class AddStockCommandHandler(
    ISkuExistenceChecker skuChecker,
    IInventoryItemRepository repository,
    IInventoryUnitOfWork unitOfWork,
    ILogger<AddStockCommandHandler> logger)
    : IRequestHandler<AddStockCommand>
{
    public async Task Handle(AddStockCommand cmd, CancellationToken cancellationToken)
    {
        var item = await repository.GetBySkuIdAsync(cmd.SkuId, cancellationToken);

        if (item is null)
        {
            if (!await skuChecker.ExistsAsync(cmd.SkuId, cancellationToken))
                throw new SkuNotFoundException(cmd.SkuId);

            item = InventoryItem.Create(cmd.SkuId, cmd.Quantity, cmd.Reason, isInitialStock: false);
            await repository.AddAsync(item, cancellationToken);
        }
        else
        {
            item.AddStock(cmd.Quantity, cmd.Reason);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Added {Quantity} units to SKU {SkuId}. OnHand={OnHand}, Reserved={Reserved}",
            cmd.Quantity, cmd.SkuId, item.QuantityOnHand, item.QuantityReserved);
    }
}
