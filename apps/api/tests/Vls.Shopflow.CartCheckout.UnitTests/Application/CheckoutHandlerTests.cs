using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Vls.Shopflow.CartCheckout.Application.CommandHandlers;
using Vls.Shopflow.CartCheckout.Application.Commands;
using Vls.Shopflow.CartCheckout.Application.Interfaces;
using Vls.Shopflow.CartCheckout.Application.Repositories;
using Vls.Shopflow.CartCheckout.Application.Services;
using Vls.Shopflow.CartCheckout.Application.Validators;
using Vls.Shopflow.CartCheckout.Domain.Entities;
using Vls.Shopflow.CartCheckout.Domain.Enums;
using Vls.Shopflow.CartCheckout.Domain.Exceptions;
using Vls.Shopflow.Inventory.Domain.Exceptions;

namespace Vls.Shopflow.CartCheckout.UnitTests.Application;

public sealed class CheckoutHandlerTests
{
  private static readonly CustomerInput Customer = new("João Silva", "joao@email.com", "11999999999");
    private static readonly AddressInput Address = new(
        "01001000", "Rua Exemplo", "123", "Apto 10", "Centro", "São Paulo", "SP");

    [Fact]
    public async Task CreateCheckoutSession_WithValidItem_CreatesSessionAndReservesStock()
    {
        var skuId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();

        var catalog = new Mock<ICatalogSkuPricingService>();
        catalog.Setup(x => x.GetBySkuIdAsync(skuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SkuPricingSnapshot(
                Guid.NewGuid(), "Produto", "produto", skuId, "SKU-001", 100m, true, true));

        var inventory = new Mock<IInventoryReservationService>();
        inventory.Setup(x => x.ReserveAsync(skuId, 2, It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservationId);

        CheckoutSession? captured = null;
        var repository = new Mock<ICheckoutSessionRepository>();
        repository.Setup(x => x.AddAsync(It.IsAny<CheckoutSession>(), It.IsAny<CancellationToken>()))
            .Callback<CheckoutSession, CancellationToken>((session, _) => captured = session)
            .Returns(Task.CompletedTask);

        var uow = new Mock<ICartCheckoutUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new CreateCheckoutSessionCommandHandler(
            catalog.Object,
            inventory.Object,
            repository.Object,
            uow.Object,
            NullLogger<CreateCheckoutSessionCommandHandler>.Instance);

        var result = await handler.Handle(
            new CreateCheckoutSessionCommand(
                Customer,
                Address,
                [new CheckoutItemInput(skuId, 2)]),
            CancellationToken.None);

        result.CheckoutSessionId.Should().NotBeEmpty();
        result.Status.Should().Be("Pending");
        result.Items.Should().ContainSingle();
        result.Items[0].UnitPrice.Should().Be(100m);
        result.Items[0].Subtotal.Should().Be(200m);
        result.Subtotal.Should().Be(200m);
        result.Total.Should().Be(200m);
        result.Payment.Status.Should().Be("NotImplemented");

        captured.Should().NotBeNull();
        captured!.Status.Should().Be(CheckoutSessionStatus.Pending);
        captured.Items.Should().ContainSingle(i => i.InventoryReservationId == reservationId);
    }

    [Fact]
    public async Task CreateCheckoutSession_IgnoresFrontendPrice_UsesCatalogPrice()
    {
        var skuId = Guid.NewGuid();

        var catalog = new Mock<ICatalogSkuPricingService>();
        catalog.Setup(x => x.GetBySkuIdAsync(skuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SkuPricingSnapshot(
                Guid.NewGuid(), "Produto", "produto", skuId, "SKU-001", 79.90m, true, true));

        var inventory = new Mock<IInventoryReservationService>();
        inventory.Setup(x => x.ReserveAsync(skuId, 1, It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        var handler = new CreateCheckoutSessionCommandHandler(
            catalog.Object,
            inventory.Object,
            Mock.Of<ICheckoutSessionRepository>(),
            Mock.Of<ICartCheckoutUnitOfWork>(),
            NullLogger<CreateCheckoutSessionCommandHandler>.Instance);

        var result = await handler.Handle(
            new CreateCheckoutSessionCommand(Customer, Address, [new CheckoutItemInput(skuId, 1)]),
            CancellationToken.None);

        result.Items[0].UnitPrice.Should().Be(79.90m);
    }

    [Fact]
    public async Task CreateCheckoutSession_WhenSkuMissing_ThrowsCatalogSkuNotFound()
    {
        var skuId = Guid.NewGuid();
        var catalog = new Mock<ICatalogSkuPricingService>();
        catalog.Setup(x => x.GetBySkuIdAsync(skuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SkuPricingSnapshot?)null);

        var handler = new CreateCheckoutSessionCommandHandler(
            catalog.Object,
            Mock.Of<IInventoryReservationService>(),
            Mock.Of<ICheckoutSessionRepository>(),
            Mock.Of<ICartCheckoutUnitOfWork>(),
            NullLogger<CreateCheckoutSessionCommandHandler>.Instance);

        var act = () => handler.Handle(
            new CreateCheckoutSessionCommand(Customer, Address, [new CheckoutItemInput(skuId, 1)]),
            CancellationToken.None);

        await act.Should().ThrowAsync<CatalogSkuNotFoundException>();
    }

    [Fact]
    public async Task CreateCheckoutSession_WhenSkuInactive_ThrowsInactiveSku()
    {
        var skuId = Guid.NewGuid();
        var catalog = new Mock<ICatalogSkuPricingService>();
        catalog.Setup(x => x.GetBySkuIdAsync(skuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SkuPricingSnapshot(
                Guid.NewGuid(), "Produto", "produto", skuId, "SKU-001", 100m, false, true));

        var handler = new CreateCheckoutSessionCommandHandler(
            catalog.Object,
            Mock.Of<IInventoryReservationService>(),
            Mock.Of<ICheckoutSessionRepository>(),
            Mock.Of<ICartCheckoutUnitOfWork>(),
            NullLogger<CreateCheckoutSessionCommandHandler>.Instance);

        var act = () => handler.Handle(
            new CreateCheckoutSessionCommand(Customer, Address, [new CheckoutItemInput(skuId, 1)]),
            CancellationToken.None);

        await act.Should().ThrowAsync<InactiveSkuException>();
    }

    [Fact]
    public async Task CreateCheckoutSession_WhenSecondReserveFails_CancelsFirstReservation()
    {
        var sku1 = Guid.NewGuid();
        var sku2 = Guid.NewGuid();
        var firstReservation = Guid.NewGuid();

        var catalog = new Mock<ICatalogSkuPricingService>();
        catalog.Setup(x => x.GetBySkuIdAsync(sku1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SkuPricingSnapshot(Guid.NewGuid(), "P1", "p1", sku1, "S1", 10m, true, true));
        catalog.Setup(x => x.GetBySkuIdAsync(sku2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SkuPricingSnapshot(Guid.NewGuid(), "P2", "p2", sku2, "S2", 20m, true, true));

        var inventory = new Mock<IInventoryReservationService>();
        inventory.Setup(x => x.ReserveAsync(sku1, 1, It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(firstReservation);
        inventory.Setup(x => x.ReserveAsync(sku2, 1, It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InsufficientStockException(sku2, 1, 0));

        var handler = new CreateCheckoutSessionCommandHandler(
            catalog.Object,
            inventory.Object,
            Mock.Of<ICheckoutSessionRepository>(),
            Mock.Of<ICartCheckoutUnitOfWork>(),
            NullLogger<CreateCheckoutSessionCommandHandler>.Instance);

        var act = () => handler.Handle(
            new CreateCheckoutSessionCommand(
                Customer,
                Address,
                [new CheckoutItemInput(sku1, 1), new CheckoutItemInput(sku2, 1)]),
            CancellationToken.None);

        await act.Should().ThrowAsync<InsufficientStockException>();
        inventory.Verify(x => x.CancelReservationAsync(firstReservation, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelCheckoutSession_CancelsReservationsAndMarksCanceled()
    {
        var reservationId = Guid.NewGuid();
        var item = CheckoutSessionItem.Create(
            Guid.NewGuid(), "Produto", "produto", Guid.NewGuid(), "SKU-001", 1, 100m, reservationId);
        var session = CheckoutSession.CreatePending(
            Customer.FullName, Customer.Email, Customer.Phone,
            Address.ZipCode, Address.Street, Address.Number, Address.Complement,
            Address.Neighborhood, Address.City, Address.State,
            DateTimeOffset.UtcNow.AddMinutes(15),
            [item]);

        var sessionId = session.Id;

        var repository = new Mock<ICheckoutSessionRepository>();
        repository.Setup(x => x.GetByIdWithItemsAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var inventory = new Mock<IInventoryReservationService>();
        var uow = new Mock<ICartCheckoutUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new CancelCheckoutSessionCommandHandler(
            repository.Object,
            inventory.Object,
            uow.Object,
            NullLogger<CancelCheckoutSessionCommandHandler>.Instance);

        await handler.Handle(new CancelCheckoutSessionCommand(sessionId), CancellationToken.None);

        inventory.Verify(x => x.CancelReservationAsync(reservationId, It.IsAny<CancellationToken>()), Times.Once);
        session.Status.Should().Be(CheckoutSessionStatus.Canceled);
    }

    [Fact]
    public async Task CancelCheckoutSession_WhenAlreadyCanceled_IsIdempotent()
    {
        var item = CheckoutSessionItem.Create(
            Guid.NewGuid(), "Produto", "produto", Guid.NewGuid(), "SKU-001", 1, 100m, Guid.NewGuid());
        var session = CheckoutSession.CreatePending(
            Customer.FullName, Customer.Email, Customer.Phone,
            Address.ZipCode, Address.Street, Address.Number, Address.Complement,
            Address.Neighborhood, Address.City, Address.State,
            DateTimeOffset.UtcNow.AddMinutes(15),
            [item]);
        session.Cancel();

        var sessionId = session.Id;

        var repository = new Mock<ICheckoutSessionRepository>();
        repository.Setup(x => x.GetByIdWithItemsAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var inventory = new Mock<IInventoryReservationService>();

        var handler = new CancelCheckoutSessionCommandHandler(
            repository.Object,
            inventory.Object,
            Mock.Of<ICartCheckoutUnitOfWork>(),
            NullLogger<CancelCheckoutSessionCommandHandler>.Instance);

        await handler.Handle(new CancelCheckoutSessionCommand(sessionId), CancellationToken.None);

        inventory.Verify(
            x => x.CancelReservationAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void ConsolidateItems_MergesDuplicateSkuIds()
    {
        var skuId = Guid.NewGuid();
        var consolidated = CheckoutItemConsolidator.Consolidate(
        [
            new CheckoutItemInput(skuId, 2),
            new CheckoutItemInput(skuId, 3)
        ]);

        consolidated.Should().ContainSingle();
        consolidated[0].Quantity.Should().Be(5);
    }

    [Fact]
    public async Task CreateCheckoutSessionValidator_RejectsEmptyItems()
    {
        var validator = new CreateCheckoutSessionCommandValidator();
        var result = await validator.ValidateAsync(
            new CreateCheckoutSessionCommand(Customer, Address, []));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task CreateCheckoutSessionValidator_RejectsNonPositiveQuantity()
    {
        var validator = new CreateCheckoutSessionCommandValidator();
        var result = await validator.ValidateAsync(
            new CreateCheckoutSessionCommand(
                Customer,
                Address,
                [new CheckoutItemInput(Guid.NewGuid(), 0)]));

        result.IsValid.Should().BeFalse();
    }
}
