using FluentAssertions;
using Vls.Shopflow.Orders.Domain.Entities;
using Vls.Shopflow.Orders.Domain.Enums;

namespace Vls.Shopflow.Orders.UnitTests.Domain;

public sealed class OrderDomainTests
{
    private static OrderItem ValidItem()
        => OrderItem.Create(Guid.NewGuid(), "Produto Demo", "SKU-001", 2, 50m);

    [Fact]
    public void CreatePendingPayment_WithValidItems_CreatesOrder()
    {
        var checkoutSessionId = Guid.NewGuid();
        var items = new[] { ValidItem() };

        var order = Order.CreatePendingPayment(
            checkoutSessionId,
            "João Silva",
            "joao@email.com",
            "11999999999",
            "01001000",
            "Rua Exemplo",
            "123",
            "Apto 10",
            "Centro",
            "São Paulo",
            "SP",
            subtotal: 100m,
            shippingAmount: null,
            total: 100m,
            items);

        order.Id.Should().NotBeEmpty();
        order.CheckoutSessionId.Should().Be(checkoutSessionId);
        order.Status.Should().Be(OrderStatus.PendingPayment);
        order.Items.Should().ContainSingle();
        order.Subtotal.Should().Be(100m);
        order.Total.Should().Be(100m);
        order.PaidAt.Should().BeNull();
        order.CanceledAt.Should().BeNull();
    }

    [Fact]
    public void CreatePendingPayment_WithoutItems_Throws()
    {
        var act = () => Order.CreatePendingPayment(
            Guid.NewGuid(),
            "João Silva",
            "joao@email.com",
            "11999999999",
            "01001000",
            "Rua Exemplo",
            "123",
            null,
            "Centro",
            "São Paulo",
            "SP",
            0m,
            null,
            0m,
            Array.Empty<OrderItem>());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*at least one item*");
    }

    [Fact]
    public void OrderItem_WithZeroQuantity_Throws()
    {
        var act = () => OrderItem.Create(Guid.NewGuid(), "Produto", "SKU", 0, 10m);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CreatePendingPayment_WithNegativeTotal_Throws()
    {
        var act = () => Order.CreatePendingPayment(
            Guid.NewGuid(),
            "João Silva",
            "joao@email.com",
            "11999999999",
            "01001000",
            "Rua Exemplo",
            "123",
            null,
            "Centro",
            "São Paulo",
            "SP",
            100m,
            null,
            -1m,
            new[] { ValidItem() });

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CreatePendingPayment_InitialStatusIsPendingPayment()
    {
        var order = Order.CreatePendingPayment(
            Guid.NewGuid(),
            "Maria",
            "maria@email.com",
            "11888888888",
            "01001000",
            "Rua A",
            "10",
            null,
            "Centro",
            "São Paulo",
            "SP",
            50m,
            null,
            50m,
            new[] { OrderItem.Create(Guid.NewGuid(), "Item", "SKU", 1, 50m) });

        order.Status.Should().Be(OrderStatus.PendingPayment);
    }
}
