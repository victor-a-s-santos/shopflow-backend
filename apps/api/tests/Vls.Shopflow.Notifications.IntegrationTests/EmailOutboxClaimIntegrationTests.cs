using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.Notifications.Domain.Entities;
using Vls.Shopflow.Notifications.Domain.Enums;
using Vls.Shopflow.Notifications.Infrastructure;
using Vls.Shopflow.Notifications.Infrastructure.Repositories;

namespace Vls.Shopflow.Notifications.IntegrationTests;

public sealed class EmailOutboxClaimIntegrationTests
{
    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("SHOPFLOW_TEST_DB")
        ?? "Host=localhost;Port=5432;Database=shopflow;Username=postgres;Password=postgres";

    private static async Task<bool> CanConnectAsync()
    {
        try
        {
            await using var db = CreateContext();
            return await db.Database.CanConnectAsync();
        }
        catch
        {
            return false;
        }
    }

    private static NotificationsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseNpgsql(ConnectionString, npg =>
                npg.MigrationsHistoryTable("__EFMigrationsHistory", "notifications"))
            .Options;
        return new NotificationsDbContext(options);
    }

    [Fact]
    public async Task ClaimPendingBatch_TwoProcessors_DoNotClaimTheSameRow()
    {
        if (!await CanConnectAsync())
            return;

        await using (var migrate = CreateContext())
            await migrate.Database.MigrateAsync();

        var key = $"order:{Guid.NewGuid():D}:created";
        var message = EmailOutboxMessage.Create(
            EmailNotificationType.OrderCreated,
            "skip-locked@shopflow.test",
            "Skip",
            "Assunto",
            "<p>hi</p>",
            "hi",
            key);

        await using (var seed = CreateContext())
        {
            seed.EmailOutboxMessages.Add(message);
            await seed.SaveChangesAsync();
        }

        await using var db1 = CreateContext();
        await using var db2 = CreateContext();
        var repo1 = new EmailOutboxRepository(db1);
        var repo2 = new EmailOutboxRepository(db2);

        var timeout = TimeSpan.FromMinutes(2);
        var claim1 = repo1.ClaimPendingBatchAsync(500, timeout, CancellationToken.None);
        var claim2 = repo2.ClaimPendingBatchAsync(500, timeout, CancellationToken.None);
        await Task.WhenAll(claim1, claim2);

        var ids1 = claim1.Result.Select(x => x.Id).ToHashSet();
        var ids2 = claim2.Result.Select(x => x.Id).ToHashSet();
        ids1.Intersect(ids2).Should().BeEmpty();
        (ids1.Contains(message.Id) || ids2.Contains(message.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task ClaimPendingBatch_ReclaimsStaleProcessing()
    {
        if (!await CanConnectAsync())
            return;

        await using (var migrate = CreateContext())
            await migrate.Database.MigrateAsync();

        var key = $"order:{Guid.NewGuid():D}:paid";
        var message = EmailOutboxMessage.Create(
            EmailNotificationType.PaymentConfirmed,
            "reclaim@shopflow.test",
            "Reclaim",
            "Assunto",
            "<p>hi</p>",
            "hi",
            key);

        await using (var seed = CreateContext())
        {
            seed.EmailOutboxMessages.Add(message);
            await seed.SaveChangesAsync();
            var staleAt = DateTimeOffset.UtcNow.AddMinutes(-10);
            await seed.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE notifications.email_outbox
                SET "Status" = {nameof(EmailOutboxStatus.Processing)},
                    "ProcessingStartedAt" = {staleAt}
                WHERE "Id" = {message.Id}
                """);
        }

        await using var db = CreateContext();
        var repo = new EmailOutboxRepository(db);
        var claimed = await repo.ClaimPendingBatchAsync(500, TimeSpan.FromMinutes(2), CancellationToken.None);

        claimed.Should().Contain(x => x.Id == message.Id);
        var reclaimed = claimed.Single(x => x.Id == message.Id);
        reclaimed.Status.Should().Be(EmailOutboxStatus.Processing);
        reclaimed.ProcessingStartedAt.Should().NotBeNull();
        reclaimed.ProcessingStartedAt.Should().BeAfter(DateTimeOffset.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task TryAddNew_DuplicateIdempotencyKey_IsSuccess()
    {
        if (!await CanConnectAsync())
            return;

        await using (var migrate = CreateContext())
            await migrate.Database.MigrateAsync();

        var key = $"order:{Guid.NewGuid():D}:shipped";
        var first = EmailOutboxMessage.Create(
            EmailNotificationType.OrderShipped,
            "dup@shopflow.test",
            "Dup",
            "Assunto",
            "<p>hi</p>",
            "hi",
            key);
        var duplicate = EmailOutboxMessage.Create(
            EmailNotificationType.OrderShipped,
            "dup@shopflow.test",
            "Dup",
            "Assunto",
            "<p>hi</p>",
            "hi",
            key);

        await using var db1 = CreateContext();
        await using var db2 = CreateContext();
        var inserted = await new EmailOutboxRepository(db1).TryAddNewAsync(first, CancellationToken.None);
        var second = await new EmailOutboxRepository(db2).TryAddNewAsync(duplicate, CancellationToken.None);

        inserted.Should().BeTrue();
        second.Should().BeFalse();

        await using var verify = CreateContext();
        (await verify.EmailOutboxMessages.CountAsync(x => x.IdempotencyKey == key)).Should().Be(1);
    }
}
