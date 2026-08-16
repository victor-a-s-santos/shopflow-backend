using Vls.Shopflow.Notifications.Domain.Entities;

namespace Vls.Shopflow.Notifications.Application.Interfaces;

public interface IEmailOutboxRepository
{
    Task<bool> ExistsByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    Task AddAsync(EmailOutboxMessage message, CancellationToken cancellationToken = default);

    /// <summary>Inserts and saves. Returns false when IdempotencyKey already exists (success/idempotent).</summary>
    Task<bool> TryAddNewAsync(EmailOutboxMessage message, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmailOutboxMessage>> ClaimPendingBatchAsync(
        int batchSize,
        TimeSpan processingTimeout,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
