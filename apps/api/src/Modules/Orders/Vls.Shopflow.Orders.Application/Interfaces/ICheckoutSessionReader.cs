namespace Vls.Shopflow.Orders.Application.Interfaces;

public sealed record CheckoutSessionItemSnapshot(
    Guid SkuId,
    string ProductName,
    string SkuCode,
    int Quantity,
    decimal UnitPrice,
    decimal Subtotal);

public sealed record CheckoutSessionSnapshot(
    Guid Id,
    string Status,
    string CustomerFullName,
    string CustomerEmail,
    string CustomerPhone,
    string ShippingZipCode,
    string ShippingStreet,
    string ShippingNumber,
    string? ShippingComplement,
    string ShippingNeighborhood,
    string ShippingCity,
    string ShippingState,
    decimal Subtotal,
    decimal? ShippingAmount,
    decimal Total,
    IReadOnlyList<CheckoutSessionItemSnapshot> Items);

public interface ICheckoutSessionReader
{
    Task<CheckoutSessionSnapshot?> GetByIdAsync(Guid checkoutSessionId, CancellationToken cancellationToken);
}
