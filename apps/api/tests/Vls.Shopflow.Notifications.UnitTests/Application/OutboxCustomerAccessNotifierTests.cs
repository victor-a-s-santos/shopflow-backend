using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Vls.Shopflow.IdentityAccess.Application.Interfaces;
using Vls.Shopflow.Notifications.Application.Interfaces;
using Vls.Shopflow.Notifications.Infrastructure.Services;

namespace Vls.Shopflow.Notifications.UnitTests.Application;

public sealed class OutboxCustomerAccessNotifierTests
{
    [Fact]
    public async Task NotifyRegisteredPending_EnqueuesAdminAndCustomerEmails()
    {
        var emails = new Mock<IEmailNotificationService>(MockBehavior.Strict);
        emails.Setup(x => x.EnqueueCustomerApprovalRequestAdminAsync(
                It.IsAny<CustomerApprovalEmailRequest>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        emails.Setup(x => x.EnqueueCustomerRegistrationReceivedAsync(
                It.IsAny<CustomerApprovalEmailRequest>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new OutboxCustomerAccessNotifier(emails.Object, NullLogger<OutboxCustomerAccessNotifier>.Instance);
        var id = Guid.NewGuid();

        await sut.NotifyRegisteredPendingAsync(
            new CustomerRegisteredPendingApproval(id, "ana@c.com", "Ana", DateTimeOffset.UtcNow, "1199"));

        emails.Verify(x => x.EnqueueCustomerApprovalRequestAdminAsync(
            It.Is<CustomerApprovalEmailRequest>(r => r.CustomerUserId == id && r.Phone == "1199"),
            It.IsAny<CancellationToken>()), Times.Once);
        emails.Verify(x => x.EnqueueCustomerRegistrationReceivedAsync(
            It.Is<CustomerApprovalEmailRequest>(r => r.CustomerUserId == id && r.Email == "ana@c.com"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotifyApproved_EnqueuesApprovedEmail()
    {
        var emails = new Mock<IEmailNotificationService>();
        var sut = new OutboxCustomerAccessNotifier(emails.Object, NullLogger<OutboxCustomerAccessNotifier>.Instance);
        var id = Guid.NewGuid();

        await sut.NotifyApprovedAsync(
            new CustomerAccessChangedNotification(id, "ana@c.com", "Ana", DateTimeOffset.UtcNow));

        emails.Verify(x => x.EnqueueCustomerApprovedAsync(
            It.Is<CustomerApprovalEmailRequest>(r => r.CustomerUserId == id && r.DecidedAt != null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotifyRejected_EnqueuesRejectedEmail()
    {
        var emails = new Mock<IEmailNotificationService>();
        var sut = new OutboxCustomerAccessNotifier(emails.Object, NullLogger<OutboxCustomerAccessNotifier>.Instance);

        await sut.NotifyRejectedAsync(new CustomerAccessChangedNotification(Guid.NewGuid(), "ana@c.com", "Ana"));

        emails.Verify(x => x.EnqueueCustomerRejectedAsync(
            It.IsAny<CustomerApprovalEmailRequest>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotifySuspended_EnqueuesSuspendedEmail()
    {
        var emails = new Mock<IEmailNotificationService>();
        var sut = new OutboxCustomerAccessNotifier(emails.Object, NullLogger<OutboxCustomerAccessNotifier>.Instance);

        await sut.NotifySuspendedAsync(new CustomerAccessChangedNotification(Guid.NewGuid(), "ana@c.com", "Ana"));

        emails.Verify(x => x.EnqueueCustomerSuspendedAsync(
            It.IsAny<CustomerApprovalEmailRequest>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotifyRegisteredPending_WhenEnqueueFails_DoesNotThrow()
    {
        var emails = new Mock<IEmailNotificationService>();
        emails.Setup(x => x.EnqueueCustomerApprovalRequestAdminAsync(
                It.IsAny<CustomerApprovalEmailRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("outbox down"));

        var sut = new OutboxCustomerAccessNotifier(emails.Object, NullLogger<OutboxCustomerAccessNotifier>.Instance);

        var act = () => sut.NotifyRegisteredPendingAsync(
            new CustomerRegisteredPendingApproval(Guid.NewGuid(), "ana@c.com", "Ana", DateTimeOffset.UtcNow));

        await act.Should().NotThrowAsync();
    }
}
