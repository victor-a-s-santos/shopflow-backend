using MediatR;
using Microsoft.Extensions.Logging;
using Vls.Shopflow.CartCheckout.Application.Commands;
using Vls.Shopflow.CartCheckout.Application.Interfaces;
using Vls.Shopflow.CartCheckout.Application.Mappers;
using Vls.Shopflow.CartCheckout.Application.Repositories;
using Vls.Shopflow.CartCheckout.Domain.Enums;
using Vls.Shopflow.CartCheckout.Domain.Exceptions;

namespace Vls.Shopflow.CartCheckout.Application.CommandHandlers;

public sealed class CancelCheckoutSessionCommandHandler(
    ICheckoutSessionRepository repository,
    IInventoryReservationService inventoryReservation,
    ICartCheckoutUnitOfWork unitOfWork,
    ILogger<CancelCheckoutSessionCommandHandler> logger)
    : IRequestHandler<CancelCheckoutSessionCommand>
{
    public async Task Handle(CancelCheckoutSessionCommand command, CancellationToken cancellationToken)
    {
        var session = await repository.GetByIdWithItemsAsync(command.CheckoutSessionId, cancellationToken)
                      ?? throw new CheckoutSessionNotFoundException(command.CheckoutSessionId);

        if (session.Status == CheckoutSessionStatus.Canceled)
            return;

        CheckoutSessionMapper.EnsureCanCancel(session);

        foreach (var item in session.Items)
        {
            await inventoryReservation.CancelReservationAsync(item.InventoryReservationId, cancellationToken);
        }

        session.Cancel();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Canceled checkout session {CheckoutSessionId}", session.Id);
    }
}
