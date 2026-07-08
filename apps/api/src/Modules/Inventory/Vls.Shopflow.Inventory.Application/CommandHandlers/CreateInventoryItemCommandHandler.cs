using MediatR;
using Microsoft.Extensions.Logging;
using Vls.Shopflow.Inventory.Application.Commands;
using Vls.Shopflow.Inventory.Application.Interfaces;
using Vls.Shopflow.Inventory.Application.Repositories;
using Vls.Shopflow.Inventory.Domain.Entities;
using Vls.Shopflow.Inventory.Domain.Exceptions;

namespace Vls.Shopflow.Inventory.Application.CommandHandlers;

public sealed class CreateInventoryItemCommandHandler(
    ISkuExistenceChecker skuChecker,
    IInventoryItemRepository repository,
    IInventoryUnitOfWork unitOfWork,
    ILogger<CreateInventoryItemCommandHandler> logger)
    : IRequestHandler<CreateInventoryItemCommand, Guid>
{
    public async Task<Guid> Handle(CreateInventoryItemCommand cmd, CancellationToken cancellationToken)
    {
        if (!await skuChecker.ExistsAsync(cmd.SkuId, cancellationToken))
            throw new SkuNotFoundException(cmd.SkuId);

        if (await repository.ExistsForSkuAsync(cmd.SkuId, cancellationToken))
            throw new InventoryItemAlreadyExistsException(cmd.SkuId);

        var item = InventoryItem.Create(cmd.SkuId, cmd.InitialQuantity, isInitialStock: true);
        await repository.AddAsync(item, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Inventory item {InventoryItemId} created for SKU {SkuId} with initial quantity {Quantity}",
            item.Id, cmd.SkuId, cmd.InitialQuantity);

        return item.Id;
    }
}
