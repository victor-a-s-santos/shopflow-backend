using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.Notifications.Application.Interfaces;
using Vls.Shopflow.Notifications.Domain.Entities;
using Vls.Shopflow.Notifications.Domain.Enums;

namespace Vls.Shopflow.Notifications.Infrastructure.Repositories;

public sealed class EmailOutboxRepository(NotificationsDbContext db) : IEmailOutboxRepository
{
    public Task<bool> ExistsByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
        => db.EmailOutboxMessages.AnyAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);

    public async Task AddAsync(EmailOutboxMessage message, CancellationToken cancellationToken = default)
        => await db.EmailOutboxMessages.AddAsync(message, cancellationToken);

    public async Task<IReadOnlyList<EmailOutboxMessage>> ClaimPendingBatchAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var batch = await db.EmailOutboxMessages
            .Where(x => x.Status == EmailOutboxStatus.Pending && x.NextAttemptAt <= now)
            .OrderBy(x => x.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        foreach (var message in batch)
            message.MarkProcessing();

        if (batch.Count > 0)
            await db.SaveChangesAsync(cancellationToken);

        return batch;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => db.SaveChangesAsync(cancellationToken);
}
