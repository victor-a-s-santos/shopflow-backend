using Vls.Shopflow.Notifications.Domain.Entities;

namespace Vls.Shopflow.Notifications.Application.Interfaces;

public interface IEmailOutboxRepository
{
    Task<bool> ExistsByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    Task AddAsync(EmailOutboxMessage message, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmailOutboxMessage>> ClaimPendingBatchAsync(
        int batchSize,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
