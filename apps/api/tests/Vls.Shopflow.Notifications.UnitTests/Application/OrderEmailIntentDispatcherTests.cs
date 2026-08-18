using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Vls.Shopflow.Notifications.Application.Interfaces;
using Vls.Shopflow.Notifications.Application.Options;
using Vls.Shopflow.Notifications.Infrastructure.Services;
using Vls.Shopflow.Orders.Application.Models;
using Vls.Shopflow.Orders.Application.Repositories;
using Vls.Shopflow.Orders.Domain.Entities;
using Vls.Shopflow.Orders.Domain.Enums;

namespace Vls.Shopflow.Notifications.UnitTests.Application;

public sealed class OrderEmailIntentDispatcherTests
{
    [Fact]
    public async Task ProcessAsync_WhenNotificationsFails_LeavesIntentPending()
    {
        var intent = CreateIntent(OrderEmailIntentType.PaymentConfirmed);
        var emails = new Mock<IEmailNotificationService>();
        emails.Setup(x => x.EnqueuePaymentConfirmedAsync(
                It.IsAny<OrderEmailNotificationRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("notifications down"));

        var dispatched = false;
        var intents = MockExecute(intent, marked => dispatched = marked);
        var sut = CreateSut(intents.Object, emails.Object, outboxExists: false);

        await sut.ProcessAsync(CancellationToken.None);

        dispatched.Should().BeFalse();
        intent.Status.Should().Be(OrderEmailIntentStatus.Pending);
    }

    [Fact]
    public async Task ProcessAsync_WhenReexecuted_EnqueuesOnceAndMarksDispatched()
    {
        var intent = CreateIntent(OrderEmailIntentType.OrderCreated);
        var emails = new Mock<IEmailNotificationService>();
        emails.Setup(x => x.EnqueueOrderCreatedAsync(
                It.IsAny<OrderEmailNotificationRequest>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var existsCalls = 0;
        var outbox = new Mock<IEmailOutboxRepository>();
        outbox.Setup(x => x.ExistsByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                existsCalls++;
                return existsCalls > 1;
            });

        var intents = MockExecute(intent, _ => { });
        var sut = new OrderEmailIntentDispatcher(
            intents.Object,
            emails.Object,
            outbox.Object,
            Options.Create(new OrderEmailIntentDispatcherOptions { Enabled = true, BatchSize = 20 }),
            NullLogger<OrderEmailIntentDispatcher>.Instance);

        await sut.ProcessAsync(CancellationToken.None);
        await sut.ProcessAsync(CancellationToken.None);

        emails.Verify(
            x => x.EnqueueOrderCreatedAsync(It.IsAny<OrderEmailNotificationRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
        intent.Status.Should().Be(OrderEmailIntentStatus.Dispatched);
    }

    [Fact]
    public async Task ProcessAsync_DoesNotLogGuestAccessToken()
    {
        var payload = new OrderEmailIntentPayload(1, "a@b.com", "A", 10m, GuestAccessToken: "super-secret-guest-token");
        var intent = OrderEmailIntent.CreatePending(
            Guid.NewGuid(),
            OrderEmailIntentType.OrderCreated,
            OrderEmailIntentPayloadJson.Serialize(payload));

        var emails = new Mock<IEmailNotificationService>();
        emails.Setup(x => x.EnqueueOrderCreatedAsync(
                It.IsAny<OrderEmailNotificationRequest>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var logger = new CollectingLogger();
        var intents = MockExecute(intent, _ => { });
        var sut = new OrderEmailIntentDispatcher(
            intents.Object,
            emails.Object,
            MockOutbox(true),
            Options.Create(new OrderEmailIntentDispatcherOptions { Enabled = true, BatchSize = 20 }),
            logger);

        await sut.ProcessAsync(CancellationToken.None);

        logger.Messages.Should().NotContain(m => m.Contains("super-secret-guest-token", StringComparison.Ordinal));
    }

    private static OrderEmailIntentDispatcher CreateSut(
        IOrderEmailIntentRepository intents,
        IEmailNotificationService emails,
        bool outboxExists)
        => new(
            intents,
            emails,
            MockOutbox(outboxExists),
            Options.Create(new OrderEmailIntentDispatcherOptions { Enabled = true, BatchSize = 20 }),
            NullLogger<OrderEmailIntentDispatcher>.Instance);

    private static IEmailOutboxRepository MockOutbox(bool exists)
    {
        var outbox = new Mock<IEmailOutboxRepository>();
        outbox.Setup(x => x.ExistsByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(exists);
        return outbox.Object;
    }

    private static Mock<IOrderEmailIntentRepository> MockExecute(
        OrderEmailIntent intent,
        Action<bool> onDispatchResult)
    {
        var intents = new Mock<IOrderEmailIntentRepository>();
        intents.Setup(x => x.RepairMissingIntentsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        intents.Setup(x => x.ExecutePendingBatchAsync(
                It.IsAny<int>(),
                It.IsAny<Func<OrderEmailIntent, CancellationToken, Task<bool>>>(),
                It.IsAny<CancellationToken>()))
            .Returns<int, Func<OrderEmailIntent, CancellationToken, Task<bool>>, CancellationToken>(
                async (_, dispatch, ct) =>
                {
                    var ok = await dispatch(intent, ct);
                    onDispatchResult(ok);
                    if (ok)
                        intent.MarkDispatched();
                });
        return intents;
    }

    private static OrderEmailIntent CreateIntent(OrderEmailIntentType type)
    {
        var payload = new OrderEmailIntentPayload(42, "a@b.com", "Ana", 99.9m);
        return OrderEmailIntent.CreatePending(Guid.NewGuid(), type, OrderEmailIntentPayloadJson.Serialize(payload));
    }

    private sealed class CollectingLogger : ILogger<OrderEmailIntentDispatcher>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
