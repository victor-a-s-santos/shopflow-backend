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

    internal static SkuSalesRuleSnapshot UnitRule()
        => new("Unit", 1, 1, null, false);

    internal static SkuPricingSnapshot Pricing(
        Guid skuId,
        decimal unitPrice = 100m,
        bool skuActive = true,
        bool productActive = true,
        SkuSalesRuleSnapshot? rule = null)
        => new(
            Guid.NewGuid(), "Produto", "produto", skuId, "SKU-001", unitPrice, skuActive, productActive,
            rule ?? UnitRule());

    [Fact]
    public async Task CreateCheckoutSession_WithValidItem_CreatesSessionAndReservesStock()
    {
        var skuId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();

        var catalog = new Mock<ICatalogSkuPricingService>();
        catalog.Setup(x => x.GetBySkuIdAsync(skuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Pricing(skuId));

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
            .ReturnsAsync(Pricing(skuId, 79.90m));

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
            .ReturnsAsync(Pricing(skuId, skuActive: false));

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
            .ReturnsAsync(Pricing(sku1, 10m));
        catalog.Setup(x => x.GetBySkuIdAsync(sku2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Pricing(sku2, 20m));

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
    public async Task CreateCheckoutSession_MinimumQuantity_RejectsBelowMin()
    {
        var skuId = Guid.NewGuid();
        var catalog = new Mock<ICatalogSkuPricingService>();
        catalog.Setup(x => x.GetBySkuIdAsync(skuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Pricing(skuId, rule: new SkuSalesRuleSnapshot("MinimumQuantity", 3, 1, null, false)));

        var handler = new CreateCheckoutSessionCommandHandler(
            catalog.Object,
            Mock.Of<IInventoryReservationService>(),
            Mock.Of<ICheckoutSessionRepository>(),
            Mock.Of<ICartCheckoutUnitOfWork>(),
            NullLogger<CreateCheckoutSessionCommandHandler>.Instance);

        var act = () => handler.Handle(
            new CreateCheckoutSessionCommand(Customer, Address, [new CheckoutItemInput(skuId, 2)]),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<CheckoutSalesRuleViolationException>();
        ex.Which.Code.Should().Be(CheckoutSalesRuleViolationException.MinQuantity);
    }

    [Fact]
    public async Task CreateCheckoutSession_MultipleQuantity_RejectsNonMultiple()
    {
        var skuId = Guid.NewGuid();
        var catalog = new Mock<ICatalogSkuPricingService>();
        catalog.Setup(x => x.GetBySkuIdAsync(skuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Pricing(skuId, rule: new SkuSalesRuleSnapshot("MultipleQuantity", 3, 3, null, false)));

        var handler = new CreateCheckoutSessionCommandHandler(
            catalog.Object,
            Mock.Of<IInventoryReservationService>(),
            Mock.Of<ICheckoutSessionRepository>(),
            Mock.Of<ICartCheckoutUnitOfWork>(),
            NullLogger<CreateCheckoutSessionCommandHandler>.Instance);

        var act = () => handler.Handle(
            new CreateCheckoutSessionCommand(Customer, Address, [new CheckoutItemInput(skuId, 4)]),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<CheckoutSalesRuleViolationException>();
        ex.Which.Code.Should().Be(CheckoutSalesRuleViolationException.QuantityStep);
    }

    [Fact]
    public async Task CreateCheckoutSession_AssortedPackage_ReservesPackagesNotPieces()
    {
        var skuId = Guid.NewGuid();
        var catalog = new Mock<ICatalogSkuPricingService>();
        catalog.Setup(x => x.GetBySkuIdAsync(skuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Pricing(skuId, rule: new SkuSalesRuleSnapshot("AssortedPackage", 1, 1, 6, true)));

        var inventory = new Mock<IInventoryReservationService>();
        inventory.Setup(x => x.ReserveAsync(skuId, 2, It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        var handler = new CreateCheckoutSessionCommandHandler(
            catalog.Object,
            inventory.Object,
            Mock.Of<ICheckoutSessionRepository>(),
            Mock.Of<ICartCheckoutUnitOfWork>(),
            NullLogger<CreateCheckoutSessionCommandHandler>.Instance);

        await handler.Handle(
            new CreateCheckoutSessionCommand(Customer, Address, [new CheckoutItemInput(skuId, 2)]),
            CancellationToken.None);

        inventory.Verify(
            x => x.ReserveAsync(skuId, 2, It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()),
            Times.Once);
        inventory.Verify(
            x => x.ReserveAsync(skuId, 12, It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateCheckoutSession_LoteFixedPackage_Quantity2_PassesAndReservesTwoNotSix()
    {
        // §11.30–31 — CORSLET lote: packageSize=3, quantity=2 → reserve 2 (not 6)
        var skuId = Guid.NewGuid();
        var catalog = new Mock<ICatalogSkuPricingService>();
        catalog.Setup(x => x.GetBySkuIdAsync(skuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Pricing(
                skuId,
                unitPrice: 241.00m,
                rule: new SkuSalesRuleSnapshot("FixedPackage", 1, 1, 3, true)));

        var inventory = new Mock<IInventoryReservationService>();
        inventory.Setup(x => x.ReserveAsync(skuId, 2, It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

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
            new CreateCheckoutSessionCommand(Customer, Address, [new CheckoutItemInput(skuId, 2)]),
            CancellationToken.None);

        result.Status.Should().Be("Pending");
        result.Items.Should().ContainSingle(i => i.Quantity == 2 && i.UnitPrice == 241.00m);
        result.Subtotal.Should().Be(482.00m);

        inventory.Verify(
            x => x.ReserveAsync(skuId, 2, It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()),
            Times.Once);
        inventory.Verify(
            x => x.ReserveAsync(skuId, 6, It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()),
            Times.Never);

        captured!.Items.Should().ContainSingle(i => i.Quantity == 2);
    }

    [Fact]
    public async Task CreateCheckoutSession_InvalidPackageConfig_Throws()
    {
        var skuId = Guid.NewGuid();
        var catalog = new Mock<ICatalogSkuPricingService>();
        catalog.Setup(x => x.GetBySkuIdAsync(skuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Pricing(skuId, rule: new SkuSalesRuleSnapshot("FixedPackage", 1, 1, null, true)));

        var handler = new CreateCheckoutSessionCommandHandler(
            catalog.Object,
            Mock.Of<IInventoryReservationService>(),
            Mock.Of<ICheckoutSessionRepository>(),
            Mock.Of<ICartCheckoutUnitOfWork>(),
            NullLogger<CreateCheckoutSessionCommandHandler>.Instance);

        var act = () => handler.Handle(
            new CreateCheckoutSessionCommand(Customer, Address, [new CheckoutItemInput(skuId, 1)]),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<CheckoutSalesRuleViolationException>();
        ex.Which.Code.Should().Be(CheckoutSalesRuleViolationException.InvalidConfiguration);
    }
}

public sealed class CheckoutSalesRuleValidatorTests
{
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(6)]
    public void MinimumQuantity_AcceptsValid(int qty)
    {
        var act = () => CheckoutSalesRuleValidator.EnsurePurchaseQuantityAllowed(
            Guid.NewGuid(), qty, new SkuSalesRuleSnapshot("MinimumQuantity", 3, 1, null, false));
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(9)]
    public void MultipleQuantity_AcceptsMultiples(int qty)
    {
        var act = () => CheckoutSalesRuleValidator.EnsurePurchaseQuantityAllowed(
            Guid.NewGuid(), qty, new SkuSalesRuleSnapshot("MultipleQuantity", 3, 3, null, false));
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public void MultipleQuantity_RejectsNonMultiples(int qty)
    {
        var act = () => CheckoutSalesRuleValidator.EnsurePurchaseQuantityAllowed(
            Guid.NewGuid(), qty, new SkuSalesRuleSnapshot("MultipleQuantity", 3, 3, null, false));
        act.Should().Throw<CheckoutSalesRuleViolationException>()
            .Which.Code.Should().Be(CheckoutSalesRuleViolationException.QuantityStep);
    }
}

public sealed class CheckoutItemConsolidatorTests
{
    [Fact]
    public void Consolidate_MergesDuplicateSkuIds()
    {
        var sku = Guid.NewGuid();
        var result = CheckoutItemConsolidator.Consolidate(
        [
            new CheckoutItemInput(sku, 1),
            new CheckoutItemInput(sku, 2)
        ]);

        result.Should().ContainSingle();
        result[0].Quantity.Should().Be(3);
    }
}

public sealed class CreateCheckoutSessionValidatorTests
{
    [Fact]
    public void EmptyItems_Fails()
    {
        var result = new CreateCheckoutSessionCommandValidator().Validate(
            new CreateCheckoutSessionCommand(
                new CustomerInput("A", "a@b.com", "1"),
                new AddressInput("01001000", "R", "1", null, "C", "S", "SP"),
                []));
        result.IsValid.Should().BeFalse();
    }
}
