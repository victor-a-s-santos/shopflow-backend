using FluentAssertions;
using Vls.Shopflow.Orders.Application.Mappers;
using Vls.Shopflow.Orders.Domain.Entities;

namespace Vls.Shopflow.Orders.UnitTests.Application;

public sealed class OrderItemSalesSnapshotTests
{
    [Fact]
    public void OrderItem_CopiesSnapshot_SalesDisplayForLote()
    {
        var orderItem = OrderItem.Create(
            Guid.NewGuid(), "Corslet", "LOTE", 2, 241m,
            new OrderItemSalesSnapshot(
                "FixedPackage", 3, "Lote com 3 peças", null, "lote(s)", true, 6, 80.33m, "2 lote(s) = 6 peças"));

        orderItem.Subtotal.Should().Be(482m);
        orderItem.TotalPieces.Should().Be(6);

        var display = OrderItemSalesDisplayMapper.ToDto(orderItem);
        display.Should().NotBeNull();
        display!.PackageSize.Should().Be(3);
        display.TotalPieces.Should().Be(6);
        display.EquivalentUnitPrice.Should().Be(80.33m);
        display.Summary.Should().Be("2 lote(s) = 6 peças");
    }

    [Fact]
    public void OrderItemSalesDisplay_Unit_ReturnsNull()
    {
        var item = OrderItem.Create(Guid.NewGuid(), "P", "SKU", 1, 10m,
            new OrderItemSalesSnapshot("Unit", null, null, null, "peça(s)", false, null, null, null));

        OrderItemSalesDisplayMapper.ToDto(item).Should().BeNull();
    }

    [Fact]
    public void OrderItemSalesDisplay_LegacyNullSnapshot_ReturnsNull()
    {
        var item = OrderItem.Create(Guid.NewGuid(), "P", "SKU", 1, 10m);
        OrderItemSalesDisplayMapper.ToDto(item).Should().BeNull();
    }

    [Fact]
    public void HistoricalSnapshot_UnaffectedByHypotheticalSkuChange()
    {
        var item = OrderItem.Create(
            Guid.NewGuid(), "Corslet", "LOTE", 2, 241m,
            new OrderItemSalesSnapshot(
                "FixedPackage", 3, "Lote com 3 peças", null, "lote(s)", true, 6, 80.33m, "2 lote(s) = 6 peças"));

        var display = OrderItemSalesDisplayMapper.ToDto(item)!;
        display.PackageSize.Should().Be(3);
        display.TotalPieces.Should().Be(6);
    }
}
