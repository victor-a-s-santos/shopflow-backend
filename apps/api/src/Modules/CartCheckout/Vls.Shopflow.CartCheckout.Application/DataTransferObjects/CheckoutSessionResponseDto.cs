namespace Vls.Shopflow.CartCheckout.Application.DataTransferObjects;

public sealed record CheckoutSessionItemDto(
    Guid SkuId,
    string ProductName,
    string SkuCode,
    int Quantity,
    decimal UnitPrice,
    decimal Subtotal);

public sealed record CheckoutPaymentDto(
    string Method,
    string Status,
    string Message);

public sealed record CheckoutSessionResponseDto(
    Guid CheckoutSessionId,
    string Status,
    IReadOnlyList<CheckoutSessionItemDto> Items,
    decimal Subtotal,
    decimal? Shipping,
    decimal Total,
    CheckoutPaymentDto Payment);
