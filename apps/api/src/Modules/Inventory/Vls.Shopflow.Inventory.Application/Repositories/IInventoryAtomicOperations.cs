namespace Vls.Shopflow.Inventory.Application.Repositories;

/// <summary>
/// Atomic stock mutations for concurrency-safe checkout operations.
/// Implemented with conditional SQL UPDATE in PostgreSQL (Infrastructure).
/// </summary>
public interface IInventoryAtomicOperations
{
    Task<Guid> ReserveAsync(Guid skuId, int quantity, DateTimeOffset? expiresAt, CancellationToken ct);

    Task RemoveStockAsync(Guid skuId, int quantity, string? reason, CancellationToken ct);

    Task ConfirmReservationAsync(Guid reservationId, CancellationToken ct);

    Task CancelReservationAsync(Guid reservationId, CancellationToken ct);
}
