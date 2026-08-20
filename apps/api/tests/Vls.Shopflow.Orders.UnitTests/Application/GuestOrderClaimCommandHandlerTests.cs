using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Vls.Shopflow.Orders.Application.CommandHandlers;
using Vls.Shopflow.Orders.Application.Commands;
using Vls.Shopflow.Orders.Application.Interfaces;
using Vls.Shopflow.Orders.Application.Repositories;
using Vls.Shopflow.Orders.Application.Validators;
using Vls.Shopflow.Orders.Domain.Entities;
using Vls.Shopflow.Orders.Domain.Exceptions;

namespace Vls.Shopflow.Orders.UnitTests.Application;

public sealed class GuestOrderClaimCommandHandlerTests
{
    private static Order CreateGuestOrder(string email = "guest@example.com")
    {
        var order = Order.CreatePendingPayment(
            Guid.NewGuid(),
            "Maria Silva",
            email,
            "11988887777",
            "01001000",
            "Rua A",
            "10",
            null,
            "Centro",
            "São Paulo",
            "SP",
            100m,
            null,
            100m,
            [OrderItem.Create(Guid.NewGuid(), "Produto", "SKU-1", 1, 100m)]);
        order.AssignOrderNumber(10582);
        return order;
    }

    private static GuestOrderAccessToken CreateToken(Guid orderId)
        => GuestOrderAccessToken.Create(orderId, "hash", DateTimeOffset.UtcNow.AddDays(7));

    [Fact]
    public async Task CreateAccount_WithValidToken_CreatesCustomerAndLinksOrder()
    {
        var order = CreateGuestOrder();
        var token = CreateToken(order.Id);
        var customerId = Guid.NewGuid();

        var gate = new Mock<IGuestOrderAccessGate>();
        gate.Setup(x => x.ValidateAsync(order.Id, "raw", It.IsAny<CancellationToken>()))
            .ReturnsAsync((token, order));

        var accounts = new Mock<ICustomerAccountPort>();
        accounts.Setup(x => x.EmailExistsAsync(order.CustomerEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        accounts.Setup(x => x.RegisterAsync(
                order.CustomerEmail, "Shopflow@123", order.CustomerFullName, order.CustomerPhone,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CustomerAccountCreateResult(true, customerId, false, []));
        accounts.Setup(x => x.SignInAsync(customerId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var uow = new Mock<IOrdersUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new CreateAccountFromGuestOrderCommandHandler(
            gate.Object, accounts.Object, uow.Object,
            NullLogger<CreateAccountFromGuestOrderCommandHandler>.Instance);

        var result = await handler.Handle(
            new CreateAccountFromGuestOrderCommand(order.Id, "raw", "Shopflow@123", "Shopflow@123"),
            CancellationToken.None);

        result.Code.Should().Be("ACCOUNT_CREATED_AND_ORDER_LINKED");
        result.OrderNumber.Should().Be("10582");
        result.CustomerCreated.Should().BeTrue();
        result.OrderLinked.Should().BeTrue();
        result.RedirectTo.Should().Be($"/account/orders/{order.Id}");
        order.CustomerUserId.Should().Be(customerId);
        token.UsageCount.Should().Be(1);

        var json = System.Text.Json.JsonSerializer.Serialize(result);
        json.Should().NotContain("guestAccessToken");
        json.Should().NotContain("TokenHash");
        json.Should().NotContain("raw");
    }

    [Fact]
    public async Task CreateAccount_WithInvalidToken_ThrowsAccessDenied()
    {
        var gate = new Mock<IGuestOrderAccessGate>();
        gate.Setup(x => x.ValidateAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GuestOrderAccessDeniedException());

        var handler = new CreateAccountFromGuestOrderCommandHandler(
            gate.Object,
            Mock.Of<ICustomerAccountPort>(),
            Mock.Of<IOrdersUnitOfWork>(),
            NullLogger<CreateAccountFromGuestOrderCommandHandler>.Instance);

        var act = () => handler.Handle(
            new CreateAccountFromGuestOrderCommand(Guid.NewGuid(), "bad", "Shopflow@123", "Shopflow@123"),
            CancellationToken.None);

        await act.Should().ThrowAsync<GuestOrderAccessDeniedException>();
    }

    [Fact]
    public async Task CreateAccount_WhenEmailExists_ThrowsAccountAlreadyExists()
    {
        var order = CreateGuestOrder();
        var token = CreateToken(order.Id);

        var gate = new Mock<IGuestOrderAccessGate>();
        gate.Setup(x => x.ValidateAsync(order.Id, "raw", It.IsAny<CancellationToken>()))
            .ReturnsAsync((token, order));

        var accounts = new Mock<ICustomerAccountPort>();
        accounts.Setup(x => x.EmailExistsAsync(order.CustomerEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new CreateAccountFromGuestOrderCommandHandler(
            gate.Object, accounts.Object, Mock.Of<IOrdersUnitOfWork>(),
            NullLogger<CreateAccountFromGuestOrderCommandHandler>.Instance);

        var act = () => handler.Handle(
            new CreateAccountFromGuestOrderCommand(order.Id, "raw", "Shopflow@123", "Shopflow@123"),
            CancellationToken.None);

        await act.Should().ThrowAsync<GuestOrderAccountAlreadyExistsException>();
        order.CustomerUserId.Should().BeNull();
    }

    [Fact]
    public async Task Claim_WithMatchingEmail_LinksOrder()
    {
        var order = CreateGuestOrder("same@example.com");
        var token = CreateToken(order.Id);
        var customerId = Guid.NewGuid();

        var gate = new Mock<IGuestOrderAccessGate>();
        gate.Setup(x => x.ValidateAsync(order.Id, "raw", It.IsAny<CancellationToken>()))
            .ReturnsAsync((token, order));

        var uow = new Mock<IOrdersUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new ClaimGuestOrderCommandHandler(
            gate.Object, uow.Object, NullLogger<ClaimGuestOrderCommandHandler>.Instance);

        var result = await handler.Handle(
            new ClaimGuestOrderCommand(order.Id, "raw", customerId, "SAME@example.com"),
            CancellationToken.None);

        result.Code.Should().Be("ORDER_LINKED");
        result.OrderNumber.Should().Be("10582");
        result.OrderLinked.Should().BeTrue();
        order.CustomerUserId.Should().Be(customerId);
        uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAccount_WeakPassword_ThrowsPasswordRequirementsNotMet()
    {
        var order = CreateGuestOrder();
        var token = CreateToken(order.Id);

        var gate = new Mock<IGuestOrderAccessGate>();
        gate.Setup(x => x.ValidateAsync(order.Id, "raw", It.IsAny<CancellationToken>()))
            .ReturnsAsync((token, order));

        var accounts = new Mock<ICustomerAccountPort>();
        accounts.Setup(x => x.EmailExistsAsync(order.CustomerEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        accounts.Setup(x => x.RegisterAsync(
                order.CustomerEmail, "Shopflow@123", order.CustomerFullName, order.CustomerPhone,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CustomerAccountCreateResult(
                false,
                null,
                false,
                [new CustomerAccountFieldError("password", "Use pelo menos um número.")]));

        var handler = new CreateAccountFromGuestOrderCommandHandler(
            gate.Object, accounts.Object, Mock.Of<IOrdersUnitOfWork>(),
            NullLogger<CreateAccountFromGuestOrderCommandHandler>.Instance);

        var act = () => handler.Handle(
            new CreateAccountFromGuestOrderCommand(order.Id, "raw", "Shopflow@123", "Shopflow@123"),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<PasswordRequirementsNotMetException>();
        ex.Which.Errors.Should().ContainSingle(e => e.Message == "Use pelo menos um número.");
        ex.Which.Message.Should().NotContain("Unable to complete registration.");
    }

    [Fact]
    public async Task Claim_WithDifferentEmail_ThrowsForbidden()
    {
        var order = CreateGuestOrder("order@example.com");
        var token = CreateToken(order.Id);

        var gate = new Mock<IGuestOrderAccessGate>();
        gate.Setup(x => x.ValidateAsync(order.Id, "raw", It.IsAny<CancellationToken>()))
            .ReturnsAsync((token, order));

        var handler = new ClaimGuestOrderCommandHandler(
            gate.Object, Mock.Of<IOrdersUnitOfWork>(),
            NullLogger<ClaimGuestOrderCommandHandler>.Instance);

        var act = () => handler.Handle(
            new ClaimGuestOrderCommand(order.Id, "raw", Guid.NewGuid(), "other@example.com"),
            CancellationToken.None);

        await act.Should().ThrowAsync<GuestOrderClaimForbiddenException>();
        order.CustomerUserId.Should().BeNull();
    }

    [Fact]
    public async Task Claim_WhenAlreadyLinkedToSameCustomer_IsIdempotent()
    {
        var customerId = Guid.NewGuid();
        var order = CreateGuestOrder();
        order.LinkToCustomerUser(customerId);
        var token = CreateToken(order.Id);

        var gate = new Mock<IGuestOrderAccessGate>();
        gate.Setup(x => x.ValidateAsync(order.Id, "raw", It.IsAny<CancellationToken>()))
            .ReturnsAsync((token, order));

        var uow = new Mock<IOrdersUnitOfWork>();
        var handler = new ClaimGuestOrderCommandHandler(
            gate.Object, uow.Object, NullLogger<ClaimGuestOrderCommandHandler>.Instance);

        var result = await handler.Handle(
            new ClaimGuestOrderCommand(order.Id, "raw", customerId, order.CustomerEmail),
            CancellationToken.None);

        result.OrderLinked.Should().BeTrue();
        uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Claim_WhenLinkedToAnotherCustomer_Throws()
    {
        var order = CreateGuestOrder();
        order.LinkToCustomerUser(Guid.NewGuid());
        var token = CreateToken(order.Id);

        var gate = new Mock<IGuestOrderAccessGate>();
        gate.Setup(x => x.ValidateAsync(order.Id, "raw", It.IsAny<CancellationToken>()))
            .ReturnsAsync((token, order));

        var handler = new ClaimGuestOrderCommandHandler(
            gate.Object, Mock.Of<IOrdersUnitOfWork>(),
            NullLogger<ClaimGuestOrderCommandHandler>.Instance);

        var act = () => handler.Handle(
            new ClaimGuestOrderCommand(order.Id, "raw", Guid.NewGuid(), order.CustomerEmail),
            CancellationToken.None);

        await act.Should().ThrowAsync<OrderAlreadyLinkedToAnotherCustomerException>();
    }

    [Fact]
    public async Task Claim_WithInvalidToken_ThrowsAccessDenied()
    {
        var gate = new Mock<IGuestOrderAccessGate>();
        gate.Setup(x => x.ValidateAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GuestOrderAccessDeniedException());

        var handler = new ClaimGuestOrderCommandHandler(
            gate.Object, Mock.Of<IOrdersUnitOfWork>(),
            NullLogger<ClaimGuestOrderCommandHandler>.Instance);

        var act = () => handler.Handle(
            new ClaimGuestOrderCommand(Guid.NewGuid(), "bad", Guid.NewGuid(), "a@b.com"),
            CancellationToken.None);

        await act.Should().ThrowAsync<GuestOrderAccessDeniedException>();
    }

    [Fact]
    public void CreateAccountValidator_PasswordMismatch_FailsOnConfirmPassword()
    {
        var result = new CreateAccountFromGuestOrderCommandValidator().Validate(
            new CreateAccountFromGuestOrderCommand(Guid.NewGuid(), "tok", "Shopflow@123", "OtherPass@1"));

        result.IsValid.Should().BeFalse();
        result.Errors.Select(e => e.PropertyName.ToLowerInvariant())
            .Should().Contain("confirmpassword");
    }

    [Fact]
    public void CreateAccountValidator_ShortPassword_Fails()
    {
        var result = new CreateAccountFromGuestOrderCommandValidator().Validate(
            new CreateAccountFromGuestOrderCommand(Guid.NewGuid(), "tok", "short", "short"));

        result.IsValid.Should().BeFalse();
        result.Errors.Select(e => e.PropertyName.ToLowerInvariant())
            .Should().Contain("password");
    }

}

public sealed class GuestOrderAccessGateTests
{
    [Fact]
    public async Task Validate_WhenTokenNotActive_Denies()
    {
        var orderId = Guid.NewGuid();

        var tokenRepo = new Mock<IGuestOrderAccessTokenRepository>();
        // Expired / revoked tokens are filtered out by the repository.
        tokenRepo.Setup(x => x.FindActiveByOrderIdAndHashAsync(
                orderId, "hash", It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuestOrderAccessToken?)null);
        tokenRepo.Setup(x => x.FindByOrderIdAndHashAsync(
                orderId, "hash", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuestOrderAccessToken?)null);

        var hasher = new Mock<IGuestOrderAccessTokenHasher>();
        hasher.Setup(x => x.Hash("raw")).Returns("hash");

        var options = Microsoft.Extensions.Options.Options.Create(
            new Vls.Shopflow.Orders.Application.Options.GuestOrderAccessOptions
            {
                Enabled = true,
                TokenHashSecret = "secret"
            });

        var gate = new Vls.Shopflow.Orders.Application.Services.GuestOrderAccessGate(
            tokenRepo.Object, hasher.Object, Mock.Of<IOrderRepository>(), options);

        var act = () => gate.ValidateAsync(orderId, "raw", CancellationToken.None);
        var ex = await act.Should().ThrowAsync<GuestOrderAccessDeniedException>();
        ex.Which.Code.Should().Be("INVALID_GUEST_ORDER_TOKEN");
    }

    [Fact]
    public async Task Validate_WhenTokenExpired_ThrowsExpired()
    {
        var orderId = Guid.NewGuid();
        var created = DateTimeOffset.UtcNow.AddDays(-10);
        var expired = GuestOrderAccessToken.Create(
            orderId, "hash", DateTimeOffset.UtcNow.AddDays(-1), createdAt: created);

        var tokenRepo = new Mock<IGuestOrderAccessTokenRepository>();
        tokenRepo.Setup(x => x.FindActiveByOrderIdAndHashAsync(
                orderId, "hash", It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuestOrderAccessToken?)null);
        tokenRepo.Setup(x => x.FindByOrderIdAndHashAsync(
                orderId, "hash", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expired);

        var hasher = new Mock<IGuestOrderAccessTokenHasher>();
        hasher.Setup(x => x.Hash("raw")).Returns("hash");

        var options = Microsoft.Extensions.Options.Options.Create(
            new Vls.Shopflow.Orders.Application.Options.GuestOrderAccessOptions
            {
                Enabled = true,
                TokenHashSecret = "secret"
            });

        var gate = new Vls.Shopflow.Orders.Application.Services.GuestOrderAccessGate(
            tokenRepo.Object, hasher.Object, Mock.Of<IOrderRepository>(), options);

        var act = () => gate.ValidateAsync(orderId, "raw", CancellationToken.None);
        await act.Should().ThrowAsync<GuestOrderAccessTokenExpiredException>();
    }
}
