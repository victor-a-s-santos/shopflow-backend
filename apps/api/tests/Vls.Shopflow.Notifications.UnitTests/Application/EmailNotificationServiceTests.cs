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
            Options.Create(new AdminNotificationsOptions { ApprovalRequestsEmail = "ops@test.local" }),
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
            Options.Create(new AdminNotificationsOptions()),
            NullLogger<EmailNotificationService>.Instance);

        await sut.EnqueuePaymentConfirmedAsync(
            new OrderEmailNotificationRequest(Guid.NewGuid(), 2, "a@b.com", "A", 10m),
            CancellationToken.None);

        outbox.Verify(x => x.TryAddNewAsync(It.IsAny<EmailOutboxMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EmailOutboxProcessor_ReleasesToPending_WhenBrevoDisabled()
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
            Options.Create(new EmailOutboxOptions { Enabled = true, MaxAttempts = 5, IntervalSeconds = 15 }),
            Options.Create(new BrevoOptions { Enabled = false }),
            NullLogger<EmailOutboxProcessor>.Instance);

        await sut.ProcessAsync(CancellationToken.None);

        message.Status.Should().Be(EmailOutboxStatus.Pending);
        message.Attempts.Should().Be(0);
        message.ProcessingStartedAt.Should().BeNull();
        message.SentAt.Should().BeNull();
        message.LastError.Should().Be("Brevo disabled (Brevo__Enabled=false).");
        message.NextAttemptAt.Should().BeAfter(DateTimeOffset.UtcNow);
        sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EmailOutboxProcessor_ReleasesToPending_WhenApiKeyMissing()
    {
        var message = EmailOutboxMessage.Create(
            EmailNotificationType.OrderCreated,
            "a@b.com",
            "A",
            "Assunto",
            "<p>hi</p>",
            "hi",
            "order:x:created-nokey");
        message.MarkProcessing();

        var outbox = new Mock<IEmailOutboxRepository>();
        outbox.Setup(x => x.ClaimPendingBatchAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([message]);
        outbox.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var sender = new Mock<ITransactionalEmailSender>(MockBehavior.Strict);

        var sut = new EmailOutboxProcessor(
            outbox.Object,
            sender.Object,
            Options.Create(new EmailOutboxOptions { Enabled = true, IntervalSeconds = 15 }),
            Options.Create(new BrevoOptions { Enabled = true, ApiKey = " ", SenderEmail = "noreply@test.com" }),
            NullLogger<EmailOutboxProcessor>.Instance);

        await sut.ProcessAsync(CancellationToken.None);

        message.Status.Should().Be(EmailOutboxStatus.Pending);
        message.Attempts.Should().Be(0);
        message.LastError.Should().Be("Brevo ApiKey is not configured.");
        sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EmailOutboxProcessor_ReleasesToPending_WhenSenderEmailMissing()
    {
        var message = EmailOutboxMessage.Create(
            EmailNotificationType.OrderCreated,
            "a@b.com",
            "A",
            "Assunto",
            "<p>hi</p>",
            "hi",
            "order:x:created-nosender");
        message.MarkProcessing();

        var outbox = new Mock<IEmailOutboxRepository>();
        outbox.Setup(x => x.ClaimPendingBatchAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([message]);
        outbox.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var sender = new Mock<ITransactionalEmailSender>(MockBehavior.Strict);

        var sut = new EmailOutboxProcessor(
            outbox.Object,
            sender.Object,
            Options.Create(new EmailOutboxOptions { Enabled = true, IntervalSeconds = 15 }),
            Options.Create(new BrevoOptions { Enabled = true, ApiKey = "k", SenderEmail = "" }),
            NullLogger<EmailOutboxProcessor>.Instance);

        await sut.ProcessAsync(CancellationToken.None);

        message.Status.Should().Be(EmailOutboxStatus.Pending);
        message.Attempts.Should().Be(0);
        message.LastError.Should().Be("Brevo SenderEmail is not configured.");
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
            Options.Create(new AdminNotificationsOptions()),
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

    [Fact]
    public async Task EnqueueCustomerApprovalRequestAdmin_UsesAdminInboxAndIdempotencyKey()
    {
        EmailOutboxMessage? saved = null;
        var outbox = CreateCapturingOutbox(m => saved = m);
        var customerId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var sut = CreateEmailService(outbox.Object, adminEmail: "ops@shop.test");

        await sut.EnqueueCustomerApprovalRequestAdminAsync(
            new CustomerApprovalEmailRequest(customerId, "ana@c.com", "Ana", "1199", DateTimeOffset.UtcNow));

        saved.Should().NotBeNull();
        saved!.RecipientEmail.Should().Be("ops@shop.test");
        saved.Type.Should().Be(EmailNotificationType.CustomerApprovalRequestAdmin);
        saved.IdempotencyKey.Should().Be($"customer:{customerId:D}:approval-request-admin");
        saved.Subject.Should().Contain("aprovação");
        saved.HtmlBody.Should().Contain("/admin/customers/approvals");
    }

    [Fact]
    public async Task EnqueueCustomerApprovalRequestAdmin_SkipsWhenAdminEmailMissing()
    {
        var outbox = new Mock<IEmailOutboxRepository>(MockBehavior.Strict);
        var sut = CreateEmailService(outbox.Object, adminEmail: " ");

        await sut.EnqueueCustomerApprovalRequestAdminAsync(
            new CustomerApprovalEmailRequest(Guid.NewGuid(), "ana@c.com", "Ana"));

        outbox.Verify(x => x.TryAddNewAsync(It.IsAny<EmailOutboxMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnqueueCustomerRegistrationReceived_UsesCustomerIdempotencyKey()
    {
        EmailOutboxMessage? saved = null;
        var outbox = CreateCapturingOutbox(m => saved = m);
        var customerId = Guid.NewGuid();
        var sut = CreateEmailService(outbox.Object);

        await sut.EnqueueCustomerRegistrationReceivedAsync(
            new CustomerApprovalEmailRequest(customerId, "ana@c.com", "Ana"));

        saved!.Type.Should().Be(EmailNotificationType.CustomerRegistrationReceived);
        saved.RecipientEmail.Should().Be("ana@c.com");
        saved.IdempotencyKey.Should().Be($"customer:{customerId:D}:registration-received");
        saved.HtmlBody.Should().Contain("em análise");
    }

    [Fact]
    public async Task EnqueueCustomerApproved_UsesApprovedKey()
    {
        EmailOutboxMessage? saved = null;
        var outbox = CreateCapturingOutbox(m => saved = m);
        var customerId = Guid.NewGuid();
        var sut = CreateEmailService(outbox.Object);

        var decided = DateTimeOffset.Parse("2026-08-16T13:00:00Z");
        await sut.EnqueueCustomerApprovedAsync(
            new CustomerApprovalEmailRequest(customerId, "ana@c.com", "Ana", DecidedAt: decided));

        saved!.IdempotencyKey.Should().Be(
            EmailNotificationService.CustomerAccessIdempotencyKey("approved", customerId, decided));
        saved.HtmlBody.Should().Contain("/login");
    }

    [Fact]
    public async Task EnqueueCustomerApproved_SecondDecisionUsesDistinctKey()
    {
        var keys = new List<string>();
        var outbox = CreateCapturingOutbox(m => keys.Add(m.IdempotencyKey));
        var sut = CreateEmailService(outbox.Object);
        var customerId = Guid.NewGuid();
        var first = DateTimeOffset.Parse("2026-08-16T13:00:00Z");
        var second = DateTimeOffset.Parse("2026-08-16T14:00:00Z");

        await sut.EnqueueCustomerApprovedAsync(
            new CustomerApprovalEmailRequest(customerId, "ana@c.com", "Ana", DecidedAt: first));
        await sut.EnqueueCustomerApprovedAsync(
            new CustomerApprovalEmailRequest(customerId, "ana@c.com", "Ana", DecidedAt: second));

        keys.Should().HaveCount(2);
        keys[0].Should().NotBe(keys[1]);
        keys[1].Should().Contain(":approved:");
    }

    [Fact]
    public async Task EnqueueCustomerRejected_DoesNotIncludeInternalReason()
    {
        EmailOutboxMessage? saved = null;
        var outbox = CreateCapturingOutbox(m => saved = m);
        var sut = CreateEmailService(outbox.Object);

        await sut.EnqueueCustomerRejectedAsync(
            new CustomerApprovalEmailRequest(Guid.NewGuid(), "ana@c.com", "Ana"));

        saved!.HtmlBody.Should().NotContain("AccessDecisionReason");
        saved.IdempotencyKey.Should().Contain(":rejected:");
    }

    [Fact]
    public async Task EnqueueCustomerSuspended_UsesSuspendedKey()
    {
        EmailOutboxMessage? saved = null;
        var outbox = CreateCapturingOutbox(m => saved = m);
        var customerId = Guid.NewGuid();
        var decided = DateTimeOffset.Parse("2026-08-16T15:00:00Z");
        var sut = CreateEmailService(outbox.Object);

        await sut.EnqueueCustomerSuspendedAsync(
            new CustomerApprovalEmailRequest(customerId, "ana@c.com", "Ana", DecidedAt: decided));

        saved!.Type.Should().Be(EmailNotificationType.CustomerSuspended);
        saved.IdempotencyKey.Should().Be(
            EmailNotificationService.CustomerAccessIdempotencyKey("suspended", customerId, decided));
        saved.HtmlBody.Should().NotContain("AccessDecisionReason");
    }

    [Fact]
    public void MaskEmail_HidesLocalPart()
    {
        EmailNotificationService.MaskEmail("ana.lojista@example.com").Should().Be("a***@example.com");
    }

    private static EmailNotificationService CreateEmailService(
        IEmailOutboxRepository outbox,
        string adminEmail = "ops@test.local")
        => new(
            outbox,
            Options.Create(new PublicAppOptions
            {
                BaseUrl = "https://loja.test",
                AdminBaseUrl = "https://admin.test"
            }),
            Options.Create(new AdminNotificationsOptions { ApprovalRequestsEmail = adminEmail }),
            NullLogger<EmailNotificationService>.Instance);

    private static Mock<IEmailOutboxRepository> CreateCapturingOutbox(Action<EmailOutboxMessage> capture)
    {
        var outbox = new Mock<IEmailOutboxRepository>();
        outbox.Setup(x => x.ExistsByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        outbox.Setup(x => x.TryAddNewAsync(It.IsAny<EmailOutboxMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EmailOutboxMessage, CancellationToken>((m, _) => capture(m))
            .ReturnsAsync(true);
        return outbox;
    }
}
