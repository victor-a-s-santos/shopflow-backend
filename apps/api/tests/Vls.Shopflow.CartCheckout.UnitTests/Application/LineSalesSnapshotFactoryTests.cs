using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging.Abstractions;
using Vls.Shopflow.CartCheckout.Application.CommandHandlers;
using Vls.Shopflow.CartCheckout.Application.Commands;
using Vls.Shopflow.CartCheckout.Application.Interfaces;
using Vls.Shopflow.CartCheckout.Application.Repositories;
using Vls.Shopflow.CartCheckout.Application.Services;
using Vls.Shopflow.CartCheckout.Domain.Entities;

namespace Vls.Shopflow.CartCheckout.UnitTests.Application;

public sealed class LineSalesSnapshotFactoryTests
{
    [Fact]
    public void Capture_Unit_HasNoPackageFields()
    {
        var snap = LineSalesSnapshotFactory.Capture(
            new SkuSalesRuleSnapshot("Unit", 1, 1, null, false), 2, 100m);

        snap.SalesMode.Should().Be("Unit");
        snap.TotalPieces.Should().BeNull();
        snap.EquivalentUnitPrice.Should().BeNull();
    }

    [Fact]
    public void Capture_FixedPackage_Corslet_TotalPiecesAndEquivalent()
    {
        var snap = LineSalesSnapshotFactory.Capture(
            new SkuSalesRuleSnapshot(
                "FixedPackage", 1, 1, 3, true, "Lote com 3 peças", null, "lote(s)", true),
            2, 241m);

        snap.TotalPieces.Should().Be(6);
        snap.EquivalentUnitPrice.Should().Be(80.33m);
        snap.SalesDisplaySummary.Should().Be("2 lote(s) = 6 peças");
    }

    [Fact]
    public void Capture_AssortedPackage_TotalPieces12()
    {
        var snap = LineSalesSnapshotFactory.Capture(
            new SkuSalesRuleSnapshot(
                "AssortedPackage", 1, 1, 6, true, "Lote sortido", null, "lote(s)", true),
            2, 120m);

        snap.TotalPieces.Should().Be(12);
    }

    [Fact]
    public async Task CreateCheckoutSession_PersistsSnapshotAndReservesPackagesNotPieces()
    {
        var skuId = Guid.NewGuid();
        var catalog = new Mock<ICatalogSkuPricingService>();
        catalog.Setup(x => x.GetBySkuIdAsync(skuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SkuPricingSnapshot(
                Guid.NewGuid(), "Corslet", "corslet", skuId, "LOTE", 241m, true, true,
                new SkuSalesRuleSnapshot(
                    "FixedPackage", 1, 1, 3, true, "Lote com 3 peças", null, "lote(s)", true)));

        var inventory = new Mock<IInventoryReservationService>();
        inventory.Setup(x => x.ReserveAsync(skuId, 2, It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        CheckoutSession? captured = null;
        var repository = new Mock<ICheckoutSessionRepository>();
        repository.Setup(x => x.AddAsync(It.IsAny<CheckoutSession>(), It.IsAny<CancellationToken>()))
            .Callback<CheckoutSession, CancellationToken>((s, _) => captured = s)
            .Returns(Task.CompletedTask);

        var uow = new Mock<ICartCheckoutUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new CreateCheckoutSessionCommandHandler(
            catalog.Object, inventory.Object, repository.Object, uow.Object,
            NullLogger<CreateCheckoutSessionCommandHandler>.Instance);

        var result = await handler.Handle(
            new CreateCheckoutSessionCommand(
                new CustomerInput("A", "a@b.com", "1"),
                new AddressInput("01001000", "R", "1", null, "C", "S", "SP"),
                [new CheckoutItemInput(skuId, 2)]),
            CancellationToken.None);

        result.Subtotal.Should().Be(482m);
        inventory.Verify(x => x.ReserveAsync(skuId, 2, It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()), Times.Once);
        inventory.Verify(x => x.ReserveAsync(skuId, 6, It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()), Times.Never);

        var item = captured!.Items.Single();
        item.TotalPieces.Should().Be(6);
        item.PackageSize.Should().Be(3);
        item.EquivalentUnitPrice.Should().Be(80.33m);
        item.SalesMode.Should().Be("FixedPackage");
        item.Subtotal.Should().Be(482m);
    }
}
