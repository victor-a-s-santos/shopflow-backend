namespace Vls.Shopflow.Orders.Domain.Entities;

/// <summary>
/// Sales-rule snapshot copied from checkout (display only).
/// </summary>
public sealed record OrderItemSalesSnapshot(
    string? SalesMode,
    int? PackageSize,
    string? PackageLabel,
    string? PackageDescription,
    string? QuantityUnitLabel,
    bool? ShowTotalPieces,
    int? TotalPieces,
    decimal? EquivalentUnitPrice,
    string? SalesDisplaySummary);

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

    public string? SalesMode { get; private set; }
    public int? PackageSize { get; private set; }
    public string? PackageLabel { get; private set; }
    public string? PackageDescription { get; private set; }
    public string? QuantityUnitLabel { get; private set; }
    public bool? ShowTotalPieces { get; private set; }
    public int? TotalPieces { get; private set; }
    public decimal? EquivalentUnitPrice { get; private set; }
    public string? SalesDisplaySummary { get; private set; }

    private OrderItem() { }

    public static OrderItem Create(
        Guid skuId,
        string productName,
        string skuCode,
        int quantity,
        decimal unitPrice,
        OrderItemSalesSnapshot? salesSnapshot = null)
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
            Subtotal = subtotal,
            SalesMode = salesSnapshot?.SalesMode,
            PackageSize = salesSnapshot?.PackageSize,
            PackageLabel = salesSnapshot?.PackageLabel,
            PackageDescription = salesSnapshot?.PackageDescription,
            QuantityUnitLabel = salesSnapshot?.QuantityUnitLabel,
            ShowTotalPieces = salesSnapshot?.ShowTotalPieces,
            TotalPieces = salesSnapshot?.TotalPieces,
            EquivalentUnitPrice = salesSnapshot?.EquivalentUnitPrice,
            SalesDisplaySummary = salesSnapshot?.SalesDisplaySummary
        };
    }

    internal void AttachToOrder(Guid orderId)
        => OrderId = orderId;
}
