using Vls.Shopflow.CartCheckout.Application.Interfaces;
using Vls.Shopflow.Inventory.Application.Repositories;

namespace Vls.Shopflow.CartCheckout.Infrastructure.Services;

/// <summary>
/// Delegates stock reservation to the Inventory module via its application port.
/// </summary>
public sealed class InventoryReservationService(IInventoryAtomicOperations atomicOperations)
    : IInventoryReservationService
{
    public Task<Guid> ReserveAsync(
        Guid skuId,
        int quantity,
        DateTimeOffset? expiresAt,
        CancellationToken cancellationToken)
        => atomicOperations.ReserveAsync(skuId, quantity, expiresAt, cancellationToken);

    public Task CancelReservationAsync(Guid reservationId, CancellationToken cancellationToken)
        => atomicOperations.CancelReservationAsync(reservationId, cancellationToken);
}
