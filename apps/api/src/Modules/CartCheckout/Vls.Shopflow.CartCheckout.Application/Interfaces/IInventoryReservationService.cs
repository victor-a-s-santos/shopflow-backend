namespace Vls.Shopflow.CartCheckout.Application.Interfaces;

public interface IInventoryReservationService
{
    Task<Guid> ReserveAsync(Guid skuId, int quantity, DateTimeOffset? expiresAt, CancellationToken cancellationToken);

    Task CancelReservationAsync(Guid reservationId, CancellationToken cancellationToken);
}
