using Microsoft.EntityFrameworkCore;
using Npgsql;
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

    public async Task<bool> TryAddNewAsync(EmailOutboxMessage message, CancellationToken cancellationToken = default)
    {
        if (await ExistsByIdempotencyKeyAsync(message.IdempotencyKey, cancellationToken))
            return false;

        await db.EmailOutboxMessages.AddAsync(message, cancellationToken);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            db.Entry(message).State = EntityState.Detached;
            return false;
        }
    }

    public async Task<IReadOnlyList<EmailOutboxMessage>> ClaimPendingBatchAsync(
        int batchSize,
        TimeSpan processingTimeout,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var staleBefore = now - (processingTimeout <= TimeSpan.Zero ? TimeSpan.FromMinutes(2) : processingTimeout);
        var pending = nameof(EmailOutboxStatus.Pending);
        var processing = nameof(EmailOutboxStatus.Processing);

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var batch = await db.EmailOutboxMessages
            .FromSqlInterpolated($"""
                SELECT * FROM notifications.email_outbox
                WHERE (
                    ("Status" = {pending} AND "NextAttemptAt" <= {now})
                    OR ("Status" = {processing} AND "ProcessingStartedAt" IS NOT NULL AND "ProcessingStartedAt" <= {staleBefore})
                )
                ORDER BY "CreatedAt"
                LIMIT {batchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);

        foreach (var message in batch)
            message.MarkProcessing();

        if (batch.Count > 0)
            await db.SaveChangesAsync(cancellationToken);

        await tx.CommitAsync(cancellationToken);
        return batch;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => db.SaveChangesAsync(cancellationToken);

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        for (var inner = ex.InnerException; inner is not null; inner = inner.InnerException)
        {
            if (inner is PostgresException pg && pg.SqlState == PostgresErrorCodes.UniqueViolation)
                return true;
        }

        return false;
    }
}
