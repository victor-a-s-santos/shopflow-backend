using MediatR;
using Microsoft.Extensions.Logging;
using Vls.Shopflow.Inventory.Application.Commands;
using Vls.Shopflow.Inventory.Application.Interfaces;
using Vls.Shopflow.Inventory.Application.Repositories;
using Vls.Shopflow.Inventory.Domain.Exceptions;

namespace Vls.Shopflow.Inventory.Application.CommandHandlers;

public sealed class ReserveStockCommandHandler(
    ISkuExistenceChecker skuChecker,
    IInventoryAtomicOperations atomicOperations,
    ILogger<ReserveStockCommandHandler> logger)
    : IRequestHandler<ReserveStockCommand, Guid>
{
    public async Task<Guid> Handle(ReserveStockCommand cmd, CancellationToken cancellationToken)
    {
        if (!await skuChecker.ExistsAsync(cmd.SkuId, cancellationToken))
            throw new SkuNotFoundException(cmd.SkuId);

        var reservationId = await atomicOperations.ReserveAsync(
            cmd.SkuId, cmd.Quantity, cmd.ExpiresAt, cancellationToken);

        logger.LogInformation(
            "Reserved {Quantity} units for SKU {SkuId}. ReservationId={ReservationId}",
            cmd.Quantity, cmd.SkuId, reservationId);

        return reservationId;
    }
}
