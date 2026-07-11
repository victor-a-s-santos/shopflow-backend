using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Vls.Shopflow.Orders.Application.Commands;
using Vls.Shopflow.Orders.Application.Interfaces;
using Vls.Shopflow.Orders.Application.Mappers;
using Vls.Shopflow.Orders.Application.Options;
using Vls.Shopflow.Orders.Application.QueryHandlers;
using Vls.Shopflow.Orders.Application.Repositories;
using Vls.Shopflow.Orders.Domain.Entities;
using Vls.Shopflow.Orders.Domain.Enums;
using Vls.Shopflow.Orders.Domain.Exceptions;

namespace Vls.Shopflow.Orders.UnitTests.Application;

public sealed class GetGuestOrderStatusQueryHandlerTests
{
    private static Order CreateOrder()
        => Order.CreatePendingPayment(
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

    private static GuestOrderAccessToken CreateToken(Guid orderId, string hash = "hash-abc")
        => GuestOrderAccessToken.Create(orderId, hash, DateTimeOffset.UtcNow.AddDays(30));

    private static GetGuestOrderStatusQueryHandler CreateHandler(
        IOrderRepository? orderRepo = null,
        IGuestOrderAccessTokenRepository? tokenRepo = null,
        IGuestOrderAccessTokenHasher? hasher = null,
        IOrderPixPaymentStatusReader? paymentReader = null,
        IOrdersUnitOfWork? uow = null,
        GuestOrderAccessOptions? options = null)
    {
        var hasherMock = new Mock<IGuestOrderAccessTokenHasher>();
        hasherMock.Setup(x => x.Hash(It.IsAny<string>())).Returns("hash-abc");

        return new GetGuestOrderStatusQueryHandler(
            orderRepo ?? Mock.Of<IOrderRepository>(),
            tokenRepo ?? Mock.Of<IGuestOrderAccessTokenRepository>(),
            hasher ?? hasherMock.Object,
            paymentReader ?? Mock.Of<IOrderPixPaymentStatusReader>(),
            uow ?? Mock.Of<IOrdersUnitOfWork>(x =>
                x.SaveChangesAsync(It.IsAny<CancellationToken>()) == Task.FromResult(1)),
            Options.Create(options ?? new GuestOrderAccessOptions { Enabled = true, TokenHashSecret = "secret" }),
            NullLogger<GetGuestOrderStatusQueryHandler>.Instance);
    }

    [Fact]
    public async Task Handle_WithValidToken_ReturnsMaskedStatusAndUpdatesUsage()
    {
        var order = CreateOrder();
        var token = CreateToken(order.Id);

        var orderRepo = new Mock<IOrderRepository>();
        orderRepo.Setup(x => x.GetByIdWithItemsAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var tokenRepo = new Mock<IGuestOrderAccessTokenRepository>();
        tokenRepo.Setup(x => x.FindActiveByOrderIdAndHashAsync(
                order.Id, "hash-abc", It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);

        var paymentReader = new Mock<IOrderPixPaymentStatusReader>();
        paymentReader.Setup(x => x.GetLatestByOrderIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderPixPaymentStatusSnapshot(
                "Pending",
                "MercadoPago",
                159.90m,
                DateTimeOffset.UtcNow.AddMinutes(30),
                null,
                DateTimeOffset.UtcNow));

        var handler = CreateHandler(orderRepo.Object, tokenRepo.Object, paymentReader: paymentReader.Object);

        var result = await handler.Handle(
            new GetGuestOrderStatusQuery(order.Id, "raw-token"),
            CancellationToken.None);

        result.OrderId.Should().Be(order.Id);
        result.OrderStatus.Should().Be("PendingPayment");
        result.Payment!.Status.Should().Be("Pending");
        result.Customer.Name.Should().Be("Vi***");
        result.Customer.Email.Should().Be("v***@gmail.com");
        result.Items.Should().ContainSingle(i => i.ProductName == "Camiseta Básica");

        var json = System.Text.Json.JsonSerializer.Serialize(result);
        json.Should().NotContain("TokenHash");
        json.Should().NotContain("guestAccessToken");
        json.Should().NotContain("ProviderOrderId");
        json.Should().NotContain("ProviderTransactionId");
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
        var handler = CreateHandler();
        var act = () => handler.Handle(new GetGuestOrderStatusQuery(Guid.NewGuid(), null), CancellationToken.None);
        await act.Should().ThrowAsync<GuestOrderAccessDeniedException>();
    }

    [Fact]
    public async Task Handle_WhenTokenInvalid_ThrowsAccessDenied()
    {
        var orderId = Guid.NewGuid();
        var tokenRepo = new Mock<IGuestOrderAccessTokenRepository>();
        tokenRepo.Setup(x => x.FindActiveByOrderIdAndHashAsync(
                orderId, "hash-abc", It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuestOrderAccessToken?)null);

        var handler = CreateHandler(tokenRepo: tokenRepo.Object);
        var act = () => handler.Handle(new GetGuestOrderStatusQuery(orderId, "bad"), CancellationToken.None);
        await act.Should().ThrowAsync<GuestOrderAccessDeniedException>();
    }

    [Fact]
    public async Task Handle_WhenTokenForOtherOrder_ThrowsAccessDenied()
    {
        var orderA = CreateOrder();
        var orderB = CreateOrder();
        var tokenForB = CreateToken(orderB.Id);

        var tokenRepo = new Mock<IGuestOrderAccessTokenRepository>();
        // Lookup is by orderA id + hash — repository returns null (token belongs to B).
        tokenRepo.Setup(x => x.FindActiveByOrderIdAndHashAsync(
                orderA.Id, "hash-abc", It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuestOrderAccessToken?)null);

        var handler = CreateHandler(tokenRepo: tokenRepo.Object);
        var act = () => handler.Handle(new GetGuestOrderStatusQuery(orderA.Id, "token-for-b"), CancellationToken.None);
        await act.Should().ThrowAsync<GuestOrderAccessDeniedException>();
        tokenForB.OrderId.Should().Be(orderB.Id);
    }

    [Fact]
    public async Task Handle_WhenOrderPaid_ReturnsPaidStatus()
    {
        var order = CreateOrder();
        order.MarkAsPaid(DateTimeOffset.UtcNow);
        var token = CreateToken(order.Id);

        var orderRepo = new Mock<IOrderRepository>();
        orderRepo.Setup(x => x.GetByIdWithItemsAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var tokenRepo = new Mock<IGuestOrderAccessTokenRepository>();
        tokenRepo.Setup(x => x.FindActiveByOrderIdAndHashAsync(
                order.Id, "hash-abc", It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);

        var paymentReader = new Mock<IOrderPixPaymentStatusReader>();
        paymentReader.Setup(x => x.GetLatestByOrderIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderPixPaymentStatusSnapshot(
                "Paid", "MercadoPago", 159.90m, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        var handler = CreateHandler(orderRepo.Object, tokenRepo.Object, paymentReader: paymentReader.Object);
        var result = await handler.Handle(new GetGuestOrderStatusQuery(order.Id, "raw"), CancellationToken.None);

        result.OrderStatus.Should().Be("Paid");
        result.Payment!.Status.Should().Be("Paid");
        result.Payment.PaidAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WhenOrderExpired_ReturnsExpired()
    {
        var order = CreateOrder();
        order.Expire();
        var token = CreateToken(order.Id);

        var orderRepo = new Mock<IOrderRepository>();
        orderRepo.Setup(x => x.GetByIdWithItemsAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var tokenRepo = new Mock<IGuestOrderAccessTokenRepository>();
        tokenRepo.Setup(x => x.FindActiveByOrderIdAndHashAsync(
                order.Id, "hash-abc", It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);

        var handler = CreateHandler(orderRepo.Object, tokenRepo.Object);
        var result = await handler.Handle(new GetGuestOrderStatusQuery(order.Id, "raw"), CancellationToken.None);
        result.OrderStatus.Should().Be("Expired");
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
}
