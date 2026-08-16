using FluentAssertions;
using Vls.Shopflow.Notifications.Domain.Entities;
using Vls.Shopflow.Notifications.Domain.Enums;

namespace Vls.Shopflow.Notifications.UnitTests.Application;

public sealed class EmailOutboxMessageLeaseTests
{
    [Fact]
    public void MarkProcessing_SetsProcessingStartedAt()
    {
        var message = Create();
        message.MarkProcessing();

        message.Status.Should().Be(EmailOutboxStatus.Processing);
        message.ProcessingStartedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkRetry_ClearsProcessingStartedAt_AndReturnsToPending()
    {
        var message = Create();
        message.MarkProcessing();
        message.MarkRetry("timeout", DateTimeOffset.UtcNow.AddMinutes(1));

        message.Status.Should().Be(EmailOutboxStatus.Pending);
        message.ProcessingStartedAt.Should().BeNull();
        message.Attempts.Should().Be(1);
    }

    [Fact]
    public void MarkSent_ClearsProcessingStartedAt()
    {
        var message = Create();
        message.MarkProcessing();
        message.MarkSent("msg-1");

        message.Status.Should().Be(EmailOutboxStatus.Sent);
        message.ProcessingStartedAt.Should().BeNull();
        message.ProviderMessageId.Should().Be("msg-1");
    }

    [Fact]
    public void ReleaseForConfigurationRetry_ReturnsToPending_WithoutConsumingAttempt()
    {
        var message = Create();
        message.MarkProcessing();
        var next = DateTimeOffset.UtcNow.AddSeconds(15);

        message.ReleaseForConfigurationRetry("Brevo ApiKey is not configured.", next);

        message.Status.Should().Be(EmailOutboxStatus.Pending);
        message.Attempts.Should().Be(0);
        message.ProcessingStartedAt.Should().BeNull();
        message.SentAt.Should().BeNull();
        message.LastError.Should().Be("Brevo ApiKey is not configured.");
        message.NextAttemptAt.Should().Be(next);
    }

    [Fact]
    public void MarkSkipped_RemainsTerminal()
    {
        var message = Create();
        message.MarkProcessing();
        message.MarkSkipped("do not send");

        message.Status.Should().Be(EmailOutboxStatus.Skipped);
        message.ProcessingStartedAt.Should().BeNull();
        message.SentAt.Should().NotBeNull();
        message.LastError.Should().Be("do not send");
    }

    private static EmailOutboxMessage Create()
        => EmailOutboxMessage.Create(
            EmailNotificationType.OrderCreated,
            "a@b.com",
            "A",
            "Assunto",
            "<p>hi</p>",
            "hi",
            "order:lease:created");
}
