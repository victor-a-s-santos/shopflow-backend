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
