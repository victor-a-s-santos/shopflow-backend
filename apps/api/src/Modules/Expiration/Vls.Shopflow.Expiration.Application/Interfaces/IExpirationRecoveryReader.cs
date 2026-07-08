namespace Vls.Shopflow.Expiration.Application.Interfaces;

public sealed record OrphanPendingOrderSnapshot(Guid OrderId, Guid CheckoutSessionId);

public interface IExpirationRecoveryReader
{
    Task<IReadOnlyList<OrphanPendingOrderSnapshot>> GetOrphanPendingOrdersBatchAsync(
        int batchSize,
        CancellationToken cancellationToken);
}
