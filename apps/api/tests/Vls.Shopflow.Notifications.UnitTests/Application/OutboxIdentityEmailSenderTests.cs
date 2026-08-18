using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Vls.Shopflow.Notifications.Application.Interfaces;
using Vls.Shopflow.Notifications.Infrastructure.Services;

namespace Vls.Shopflow.Notifications.UnitTests.Application;

public sealed class OutboxIdentityEmailSenderTests
{
    [Fact]
    public async Task SendPasswordReset_WhenEnqueueFails_DoesNotThrow_AndDoesNotLogToken()
    {
        var emails = new Mock<IEmailNotificationService>();
        emails.Setup(x => x.EnqueueResetPasswordAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("outbox down"));

        var logger = new CollectingLogger();
        var sut = new OutboxIdentityEmailSender(emails.Object, logger);

        var act = () => sut.SendPasswordResetAsync("user@test.local", "reset-token-secret-value", CancellationToken.None);

        await act.Should().NotThrowAsync();
        logger.Messages.Should().NotContain(m => m.Contains("reset-token-secret-value", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SendEmailConfirmation_WhenEnqueueFails_DoesNotThrow_AndDoesNotLogToken()
    {
        var emails = new Mock<IEmailNotificationService>();
        emails.Setup(x => x.EnqueueConfirmEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("outbox down"));

        var logger = new CollectingLogger();
        var sut = new OutboxIdentityEmailSender(emails.Object, logger);

        var act = () => sut.SendEmailConfirmationAsync("user@test.local", "confirm-token-secret-value", CancellationToken.None);

        await act.Should().NotThrowAsync();
        logger.Messages.Should().NotContain(m => m.Contains("confirm-token-secret-value", StringComparison.Ordinal));
    }

    private sealed class CollectingLogger : ILogger<OutboxIdentityEmailSender>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));
    }
}
