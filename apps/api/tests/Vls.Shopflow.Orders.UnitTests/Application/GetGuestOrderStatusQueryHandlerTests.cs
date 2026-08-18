using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Vls.Shopflow.Orders.Application.Commands;
using Vls.Shopflow.Orders.Application.Interfaces;
using Vls.Shopflow.Orders.Application.Mappers;
using Vls.Shopflow.Orders.Application.QueryHandlers;
using Vls.Shopflow.Orders.Application.Repositories;
using Vls.Shopflow.Orders.Application.Services;
using Vls.Shopflow.Orders.Domain.Entities;
using Vls.Shopflow.Orders.Domain.Enums;
using Vls.Shopflow.Orders.Domain.Exceptions;

namespace Vls.Shopflow.Orders.UnitTests.Application;

public sealed class GetGuestOrderStatusQueryHandlerTests
{
    private static Order CreateOrder()
    {
        var order = Order.CreatePendingPayment(
            Guid.NewGuid(),
            "Victor Araujo",
            "victor@gmail.com",
            "11999999999",
            "01001000",
            "Rua Secreta",
            "100",
            null,
            "Centro",
            "São Paulo",
            "SP",
            159.90m,
            0m,
            159.90m,
            new[] { OrderItem.Create(Guid.NewGuid(), "Camiseta Básica", "SKU-M", 1, 159.90m) });
        order.AssignOrderNumber(10582);
        return order;
    }

    private static GuestOrderAccessToken CreateToken(Guid orderId, string hash = "hash-abc")
        => GuestOrderAccessToken.Create(orderId, hash, DateTimeOffset.UtcNow.AddDays(30));

    private static GetGuestOrderStatusQueryHandler CreateGuestHandler(
        IGuestOrderAccessGate? gate = null,
        IOrderPixPaymentStatusReader? paymentReader = null,
        ICustomerAccountPort? accounts = null,
        IOrdersUnitOfWork? uow = null)
    {
        var accountPort = accounts ?? Mock.Of<ICustomerAccountPort>(x =>
            x.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()) == Task.FromResult(false));

        return new GetGuestOrderStatusQueryHandler(
            gate ?? Mock.Of<IGuestOrderAccessGate>(),
            paymentReader ?? Mock.Of<IOrderPixPaymentStatusReader>(),
            accountPort,
            uow ?? Mock.Of<IOrdersUnitOfWork>(x =>
                x.SaveChangesAsync(It.IsAny<CancellationToken>()) == Task.FromResult(1)),
            NullLogger<GetGuestOrderStatusQueryHandler>.Instance);
    }

    private static GetPublicOrderStatusQueryHandler CreatePublicHandler(
        IGuestOrderAccessGate? gate = null,
        IOrderPixPaymentStatusReader? paymentReader = null,
        ICustomerAccountPort? accounts = null,
        IOrdersUnitOfWork? uow = null)
    {
        var accountPort = accounts ?? Mock.Of<ICustomerAccountPort>(x =>
            x.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()) == Task.FromResult(false));

        return new GetPublicOrderStatusQueryHandler(
            gate ?? Mock.Of<IGuestOrderAccessGate>(),
            paymentReader ?? Mock.Of<IOrderPixPaymentStatusReader>(),
            accountPort,
            uow ?? Mock.Of<IOrdersUnitOfWork>(x =>
                x.SaveChangesAsync(It.IsAny<CancellationToken>()) == Task.FromResult(1)),
            NullLogger<GetPublicOrderStatusQueryHandler>.Instance);
    }

    [Fact]
    public async Task Handle_WithValidToken_ReturnsMaskedStatusAndUpdatesUsage()
    {
        var order = CreateOrder();
        var token = CreateToken(order.Id);
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(30);

        var gate = new Mock<IGuestOrderAccessGate>();
        gate.Setup(x => x.ValidateAsync(order.Id, "raw-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync((token, order));

        var paymentReader = new Mock<IOrderPixPaymentStatusReader>();
        paymentReader.Setup(x => x.GetLatestByOrderIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderPixPaymentStatusSnapshot(
                "Pending",
                "MercadoPago",
                159.90m,
                expiresAt,
                null,
                DateTimeOffset.UtcNow));

        var handler = CreateGuestHandler(gate.Object, paymentReader.Object);

        var result = await handler.Handle(
            new GetGuestOrderStatusQuery(order.Id, "raw-token"),
            CancellationToken.None);

        result.OrderId.Should().Be(order.Id);
        result.OrderNumber.Should().Be("10582");
        result.CustomerStatus.Should().Be(OrderCustomerStatusCodes.AwaitingPayment);
        result.OrderStatus.Should().Be("PendingPayment");
        result.Payment!.Status.Should().Be("Pending");
        result.Payment.Method.Should().Be("Pix");
        result.Payment.ExpiresAt.Should().Be(expiresAt);
        result.Customer.Name.Should().Be("Vi***");
        result.Customer.Email.Should().Be("v***@gmail.com");
        result.CanCreateAccount.Should().BeTrue();
        result.AccountExistsForEmail.Should().BeFalse();
        result.Items.Should().ContainSingle(i => i.ProductName == "Camiseta Básica");

        var json = System.Text.Json.JsonSerializer.Serialize(result);
        json.Should().NotContain("TokenHash");
        json.Should().NotContain("guestAccessToken");
        json.Should().NotContain("ProviderOrderId");
        json.Should().NotContain("ProviderTransactionId");
        json.Should().NotContain("MercadoPago");
        json.Should().NotContain("Rua Secreta");
        json.Should().NotContain("11999999999");
        json.Should().NotContain("hash-abc");
        json.Should().NotContain("raw-token");

        token.UsageCount.Should().Be(1);
        token.LastUsedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WhenTokenMissing_ThrowsAccessDenied()
    {
        var gate = new Mock<IGuestOrderAccessGate>();
        gate.Setup(x => x.ValidateAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GuestOrderAccessDeniedException());

        var handler = CreateGuestHandler(gate.Object);
        var act = () => handler.Handle(new GetGuestOrderStatusQuery(Guid.NewGuid(), null), CancellationToken.None);
        await act.Should().ThrowAsync<GuestOrderAccessDeniedException>();
    }

    [Fact]
    public async Task Handle_WhenTokenInvalid_ThrowsAccessDenied()
    {
        var orderId = Guid.NewGuid();
        var gate = new Mock<IGuestOrderAccessGate>();
        gate.Setup(x => x.ValidateAsync(orderId, "bad", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GuestOrderAccessDeniedException());

        var handler = CreateGuestHandler(gate.Object);
        var act = () => handler.Handle(new GetGuestOrderStatusQuery(orderId, "bad"), CancellationToken.None);
        await act.Should().ThrowAsync<GuestOrderAccessDeniedException>();
    }

    [Fact]
    public async Task Handle_WhenOrderPaid_OmitsActiveExpiration()
    {
        var order = CreateOrder();
        order.MarkAsPaid(DateTimeOffset.UtcNow);
        var token = CreateToken(order.Id);
        var paidAt = DateTimeOffset.UtcNow;

        var gate = new Mock<IGuestOrderAccessGate>();
        gate.Setup(x => x.ValidateAsync(order.Id, "raw", It.IsAny<CancellationToken>()))
            .ReturnsAsync((token, order));

        var paymentReader = new Mock<IOrderPixPaymentStatusReader>();
        paymentReader.Setup(x => x.GetLatestByOrderIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderPixPaymentStatusSnapshot(
                "Paid",
                "MercadoPago",
                159.90m,
                DateTimeOffset.UtcNow.AddMinutes(-5),
                paidAt,
                DateTimeOffset.UtcNow));

        var handler = CreateGuestHandler(gate.Object, paymentReader.Object);
        var result = await handler.Handle(new GetGuestOrderStatusQuery(order.Id, "raw"), CancellationToken.None);

        result.CustomerStatus.Should().Be(OrderCustomerStatusCodes.Confirmed);
        result.OrderStatus.Should().Be("Paid");
        result.Payment!.Status.Should().Be("Paid");
        result.Payment.PaidAt.Should().Be(paidAt);
        result.Payment.ExpiresAt.Should().BeNull();
        result.Payment.Method.Should().Be("Pix");
    }

    [Fact]
    public async Task Handle_WhenOrderExpired_ReturnsExpired()
    {
        var order = CreateOrder();
        order.Expire();
        var token = CreateToken(order.Id);

        var gate = new Mock<IGuestOrderAccessGate>();
        gate.Setup(x => x.ValidateAsync(order.Id, "raw", It.IsAny<CancellationToken>()))
            .ReturnsAsync((token, order));

        var handler = CreateGuestHandler(gate.Object);
        var result = await handler.Handle(new GetGuestOrderStatusQuery(order.Id, "raw"), CancellationToken.None);
        result.CustomerStatus.Should().Be(OrderCustomerStatusCodes.Expired);
        result.OrderStatus.Should().Be("Expired");
    }

    [Fact]
    public async Task PublicHandle_WithValidOrderNumberAndToken_ReturnsStatus()
    {
        var order = CreateOrder();
        var token = CreateToken(order.Id);

        var gate = new Mock<IGuestOrderAccessGate>();
        gate.Setup(x => x.ValidateByOrderNumberAsync(10582, "raw-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync((token, order));

        var paymentReader = new Mock<IOrderPixPaymentStatusReader>();
        paymentReader.Setup(x => x.GetLatestByOrderIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderPixPaymentStatusSnapshot(
                "Pending", "Fake", 159.90m, DateTimeOffset.UtcNow.AddMinutes(10), null, DateTimeOffset.UtcNow));

        var handler = CreatePublicHandler(gate.Object, paymentReader.Object);
        var result = await handler.Handle(new GetPublicOrderStatusQuery("10582", "raw-token"), CancellationToken.None);

        result.OrderNumber.Should().Be("10582");
        result.CustomerStatus.Should().Be(OrderCustomerStatusCodes.AwaitingPayment);
        gate.Verify(x => x.ValidateByOrderNumberAsync(10582, "raw-token", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublicHandle_WithInvalidOrderNumber_ThrowsAccessDenied()
    {
        var handler = CreatePublicHandler();
        var act = () => handler.Handle(new GetPublicOrderStatusQuery("not-a-number", "token"), CancellationToken.None);
        await act.Should().ThrowAsync<GuestOrderAccessDeniedException>();
    }

    [Fact]
    public async Task PublicHandle_WhenTokenBelongsToOtherOrder_ThrowsAccessDenied()
    {
        var gate = new Mock<IGuestOrderAccessGate>();
        gate.Setup(x => x.ValidateByOrderNumberAsync(10582, "other-order-token", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GuestOrderAccessDeniedException());

        var handler = CreatePublicHandler(gate.Object);
        var act = () => handler.Handle(new GetPublicOrderStatusQuery("10582", "other-order-token"), CancellationToken.None);
        await act.Should().ThrowAsync<GuestOrderAccessDeniedException>();
    }

    [Fact]
    public void MaskNameAndEmail_FollowPromptExamples()
    {
        OrderMapper.MaskName("Victor").Should().Be("Vi***");
        OrderMapper.MaskEmail("victor@gmail.com").Should().Be("v***@gmail.com");
    }

    [Fact]
    public void GuestOrderAccessToken_Revoke_MakesInactive()
    {
        var token = CreateToken(Guid.NewGuid());
        token.IsActive(DateTimeOffset.UtcNow).Should().BeTrue();
        token.Revoke();
        token.IsActive(DateTimeOffset.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void GuestOrderAccessToken_IsActive_FalseWhenPastExpiry()
    {
        var token = CreateToken(Guid.NewGuid());
        typeof(GuestOrderAccessToken).GetProperty(nameof(GuestOrderAccessToken.ExpiresAt))!
            .SetValue(token, DateTimeOffset.UtcNow.AddMinutes(-1));

        token.IsActive(DateTimeOffset.UtcNow).Should().BeFalse();
    }

    [Theory]
    [InlineData(OrderStatus.PendingPayment, null, OrderCustomerStatusCodes.AwaitingPayment)]
    [InlineData(OrderStatus.PendingPayment, "Pending", OrderCustomerStatusCodes.AwaitingPayment)]
    [InlineData(OrderStatus.PendingPayment, "Paid", OrderCustomerStatusCodes.Confirmed)]
    [InlineData(OrderStatus.PendingPayment, "Expired", OrderCustomerStatusCodes.Expired)]
    [InlineData(OrderStatus.Paid, "Paid", OrderCustomerStatusCodes.Confirmed)]
    [InlineData(OrderStatus.Canceled, null, OrderCustomerStatusCodes.Canceled)]
    [InlineData(OrderStatus.Expired, null, OrderCustomerStatusCodes.Expired)]
    public void CustomerStatusProjector_MapsExpectedCodes(
        OrderStatus orderStatus,
        string? paymentStatus,
        string expected)
        => OrderCustomerStatusProjector.Project(orderStatus, paymentStatus).Should().Be(expected);
}
