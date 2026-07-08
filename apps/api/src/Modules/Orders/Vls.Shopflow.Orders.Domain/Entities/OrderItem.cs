namespace Vls.Shopflow.Orders.Domain.Entities;

public sealed class OrderItem
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid SkuId { get; private set; }
    public string ProductName { get; private set; } = default!;
    public string SkuCode { get; private set; } = default!;
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal Subtotal { get; private set; }

    private OrderItem() { }

    public static OrderItem Create(
        Guid skuId,
        string productName,
        string skuCode,
        int quantity,
        decimal unitPrice)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Order item quantity must be greater than zero.");

        if (unitPrice < 0)
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "Order item unit price cannot be negative.");

        var subtotal = unitPrice * quantity;
        if (subtotal < 0)
            throw new ArgumentOutOfRangeException(nameof(subtotal), "Order item subtotal cannot be negative.");

        return new OrderItem
        {
            Id = Guid.NewGuid(),
            SkuId = skuId,
            ProductName = productName.Trim(),
            SkuCode = skuCode.Trim(),
            Quantity = quantity,
            UnitPrice = unitPrice,
            Subtotal = subtotal
        };
    }

    internal void AttachToOrder(Guid orderId)
        => OrderId = orderId;
}
