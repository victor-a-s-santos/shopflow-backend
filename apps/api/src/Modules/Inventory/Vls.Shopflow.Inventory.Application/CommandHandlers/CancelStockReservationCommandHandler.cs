using MediatR;
using Microsoft.Extensions.Logging;
using Vls.Shopflow.Inventory.Application.Commands;
using Vls.Shopflow.Inventory.Application.Repositories;

namespace Vls.Shopflow.Inventory.Application.CommandHandlers;

public sealed class CancelStockReservationCommandHandler(
    IInventoryAtomicOperations atomicOperations,
    ILogger<CancelStockReservationCommandHandler> logger)
    : IRequestHandler<CancelStockReservationCommand>
{
    public async Task Handle(CancelStockReservationCommand cmd, CancellationToken cancellationToken)
    {
        await atomicOperations.CancelReservationAsync(cmd.ReservationId, cancellationToken);

        logger.LogInformation(
            "Canceled reservation {ReservationId}",
            cmd.ReservationId);
    }
}
