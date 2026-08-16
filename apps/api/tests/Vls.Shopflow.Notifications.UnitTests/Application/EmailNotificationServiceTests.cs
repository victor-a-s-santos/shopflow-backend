using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Vls.Shopflow.Notifications.Application.Interfaces;
using Vls.Shopflow.Notifications.Application.Models;
using Vls.Shopflow.Notifications.Application.Options;
using Vls.Shopflow.Notifications.Application.Services;
using Vls.Shopflow.Notifications.Domain.Entities;
using Vls.Shopflow.Notifications.Domain.Enums;

namespace Vls.Shopflow.Notifications.UnitTests.Application;

public sealed class EmailNotificationServiceTests
{
    [Fact]
    public async Task EnqueueOrderCreated_UsesIdempotencyKey()
    {
        EmailOutboxMessage? saved = null;
        var outbox = new Mock<IEmailOutboxRepository>();
        outbox.Setup(x => x.ExistsByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        outbox.Setup(x => x.TryAddNewAsync(It.IsAny<EmailOutboxMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EmailOutboxMessage, CancellationToken>((m, _) => saved = m)
            .ReturnsAsync(true);

        var sut = new EmailNotificationService(
            outbox.Object,
            Options.Create(new PublicAppOptions { BaseUrl = "https://loja.test" }),
            NullLogger<EmailNotificationService>.Instance);

        var orderId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await sut.EnqueueOrderCreatedAsync(
            new OrderEmailNotificationRequest(orderId, 1, "a@b.com", "A", 10m),
            CancellationToken.None);

        saved.Should().NotBeNull();
        saved!.IdempotencyKey.Should().Be($"order:{orderId:D}:created");
        saved.Type.Should().Be(EmailNotificationType.OrderCreated);
        saved.Subject.Should().Contain("#1");
    }

    [Fact]
    public async Task Enqueue_SkipsWhenIdempotencyKeyExists()
    {
        var outbox = new Mock<IEmailOutboxRepository>();
        outbox.Setup(x => x.ExistsByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = new EmailNotificationService(
            outbox.Object,
            Options.Create(new PublicAppOptions()),
            NullLogger<EmailNotificationService>.Instance);

        await sut.EnqueuePaymentConfirmedAsync(
            new OrderEmailNotificationRequest(Guid.NewGuid(), 2, "a@b.com", "A", 10m),
            CancellationToken.None);

        outbox.Verify(x => x.TryAddNewAsync(It.IsAny<EmailOutboxMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EmailOutboxProcessor_MarksSkipped_WhenBrevoDisabled()
    {
        var message = EmailOutboxMessage.Create(
            EmailNotificationType.OrderCreated,
            "a@b.com",
            "A",
            "Assunto",
            "<p>hi</p>",
            "hi",
            "order:x:created");
        message.MarkProcessing();

        var outbox = new Mock<IEmailOutboxRepository>();
        outbox.Setup(x => x.ClaimPendingBatchAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([message]);
        outbox.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var sender = new Mock<ITransactionalEmailSender>(MockBehavior.Strict);

        var sut = new EmailOutboxProcessor(
            outbox.Object,
            sender.Object,
            Options.Create(new EmailOutboxOptions { Enabled = true, MaxAttempts = 5 }),
            Options.Create(new BrevoOptions { Enabled = false }),
            NullLogger<EmailOutboxProcessor>.Instance);

        await sut.ProcessAsync(CancellationToken.None);

        message.Status.Should().Be(EmailOutboxStatus.Skipped);
        sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EmailOutboxProcessor_SchedulesRetry_OnTransientFailure()
    {
        var message = EmailOutboxMessage.Create(
            EmailNotificationType.OrderCreated,
            "a@b.com",
            "A",
            "Assunto",
            "<p>hi</p>",
            "hi",
            "order:y:created");
        message.MarkProcessing();

        var outbox = new Mock<IEmailOutboxRepository>();
        outbox.Setup(x => x.ClaimPendingBatchAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([message]);
        outbox.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var sender = new Mock<ITransactionalEmailSender>();
        sender.Setup(x => x.SendAsync(It.IsAny<TransactionalEmailMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TransactionalEmailSendResult(false, null, "500", true));

        var sut = new EmailOutboxProcessor(
            outbox.Object,
            sender.Object,
            Options.Create(new EmailOutboxOptions { Enabled = true, MaxAttempts = 5 }),
            Options.Create(new BrevoOptions { Enabled = true, ApiKey = "k", SenderEmail = "a@b.com" }),
            NullLogger<EmailOutboxProcessor>.Instance);

        await sut.ProcessAsync(CancellationToken.None);

        message.Status.Should().Be(EmailOutboxStatus.Pending);
        message.Attempts.Should().Be(1);
        message.NextAttemptAt.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task EmailOutboxProcessor_MarksFailed_WhenMaxAttemptsReached()
    {
        var message = EmailOutboxMessage.Create(
            EmailNotificationType.OrderCreated,
            "a@b.com",
            "A",
            "Assunto",
            "<p>hi</p>",
            "hi",
            "order:z:created");
        message.MarkProcessing();
        // Simulate previous attempts exhausted almost
        message.MarkRetry("prev", DateTimeOffset.UtcNow.AddMinutes(-1));
        message.MarkProcessing();
        // Attempts is 1 now; set MaxAttempts = 1 so next failure is permanent
        // Actually MarkRetry already set Attempts=1. With MaxAttempts=1: Attempts+1=2 < 1? false → Failed

        var outbox = new Mock<IEmailOutboxRepository>();
        outbox.Setup(x => x.ClaimPendingBatchAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([message]);
        outbox.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var sender = new Mock<ITransactionalEmailSender>();
        sender.Setup(x => x.SendAsync(It.IsAny<TransactionalEmailMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TransactionalEmailSendResult(false, null, "boom", true));

        var sut = new EmailOutboxProcessor(
            outbox.Object,
            sender.Object,
            Options.Create(new EmailOutboxOptions { Enabled = true, MaxAttempts = 1, ProcessingTimeoutSeconds = 120 }),
            Options.Create(new BrevoOptions { Enabled = true, ApiKey = "k", SenderEmail = "a@b.com" }),
            NullLogger<EmailOutboxProcessor>.Instance);

        await sut.ProcessAsync(CancellationToken.None);

        message.Status.Should().Be(EmailOutboxStatus.Failed);
    }

    [Fact]
    public async Task Enqueue_WhenTryAddReturnsFalse_TreatsAsIdempotentSuccess()
    {
        var outbox = new Mock<IEmailOutboxRepository>();
        outbox.Setup(x => x.ExistsByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        outbox.Setup(x => x.TryAddNewAsync(It.IsAny<EmailOutboxMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var sut = new EmailNotificationService(
            outbox.Object,
            Options.Create(new PublicAppOptions { BaseUrl = "https://loja.test" }),
            NullLogger<EmailNotificationService>.Instance);

        var act = () => sut.EnqueuePaymentConfirmedAsync(
            new OrderEmailNotificationRequest(Guid.NewGuid(), 2, "a@b.com", "A", 10m),
            CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EmailOutboxProcessor_PassesProcessingTimeoutToClaim()
    {
        TimeSpan? captured = null;
        var outbox = new Mock<IEmailOutboxRepository>();
        outbox.Setup(x => x.ClaimPendingBatchAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Callback<int, TimeSpan, CancellationToken>((_, timeout, _) => captured = timeout)
            .ReturnsAsync([]);

        var sut = new EmailOutboxProcessor(
            outbox.Object,
            Mock.Of<ITransactionalEmailSender>(),
            Options.Create(new EmailOutboxOptions { Enabled = true, ProcessingTimeoutSeconds = 90 }),
            Options.Create(new BrevoOptions { Enabled = true, ApiKey = "k", SenderEmail = "a@b.com" }),
            NullLogger<EmailOutboxProcessor>.Instance);

        await sut.ProcessAsync(CancellationToken.None);

        captured.Should().Be(TimeSpan.FromSeconds(90));
    }
}
