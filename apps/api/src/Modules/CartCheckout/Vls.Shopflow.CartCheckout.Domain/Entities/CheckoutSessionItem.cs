namespace Vls.Shopflow.CartCheckout.Domain.Entities;

/// <summary>
/// Sales-rule snapshot captured at checkout (display only; not used for stock/payment).
/// </summary>
public sealed record CheckoutItemSalesSnapshot(
    string SalesMode,
    int? PackageSize,
    string? PackageLabel,
    string? PackageDescription,
    string? QuantityUnitLabel,
    bool? ShowTotalPieces,
    int? TotalPieces,
    decimal? EquivalentUnitPrice,
    string? SalesDisplaySummary);

public sealed class CheckoutSessionItem
{
    public Guid Id { get; private set; }
    public Guid CheckoutSessionId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = default!;
    public string ProductSlug { get; private set; } = default!;
    public Guid SkuId { get; private set; }
    public string SkuCode { get; private set; } = default!;
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal Subtotal { get; private set; }
    public Guid InventoryReservationId { get; private set; }

    public string? SalesMode { get; private set; }
    public int? PackageSize { get; private set; }
    public string? PackageLabel { get; private set; }
    public string? PackageDescription { get; private set; }
    public string? QuantityUnitLabel { get; private set; }
    public bool? ShowTotalPieces { get; private set; }
    public int? TotalPieces { get; private set; }
    public decimal? EquivalentUnitPrice { get; private set; }
    public string? SalesDisplaySummary { get; private set; }

    private CheckoutSessionItem() { }

    public static CheckoutSessionItem Create(
        Guid productId,
        string productName,
        string productSlug,
        Guid skuId,
        string skuCode,
        int quantity,
        decimal unitPrice,
        Guid inventoryReservationId,
        CheckoutItemSalesSnapshot? salesSnapshot = null)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity));

        var subtotal = unitPrice * quantity;

        return new CheckoutSessionItem
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            ProductName = productName,
            ProductSlug = productSlug,
            SkuId = skuId,
            SkuCode = skuCode,
            Quantity = quantity,
            UnitPrice = unitPrice,
            Subtotal = subtotal,
            InventoryReservationId = inventoryReservationId,
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

    internal void AttachToSession(Guid checkoutSessionId)
        => CheckoutSessionId = checkoutSessionId;
}
