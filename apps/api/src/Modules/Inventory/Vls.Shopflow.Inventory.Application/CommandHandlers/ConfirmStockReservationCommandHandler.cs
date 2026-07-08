using MediatR;
using Microsoft.Extensions.Logging;
using Vls.Shopflow.Inventory.Application.Commands;
using Vls.Shopflow.Inventory.Application.Repositories;

namespace Vls.Shopflow.Inventory.Application.CommandHandlers;

public sealed class ConfirmStockReservationCommandHandler(
    IInventoryAtomicOperations atomicOperations,
    ILogger<ConfirmStockReservationCommandHandler> logger)
    : IRequestHandler<ConfirmStockReservationCommand>
{
    public async Task Handle(ConfirmStockReservationCommand cmd, CancellationToken cancellationToken)
    {
        await atomicOperations.ConfirmReservationAsync(cmd.ReservationId, cancellationToken);

        logger.LogInformation(
            "Confirmed reservation {ReservationId}",
            cmd.ReservationId);
    }
}
