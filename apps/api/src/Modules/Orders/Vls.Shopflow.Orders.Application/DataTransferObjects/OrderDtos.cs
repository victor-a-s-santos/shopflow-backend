namespace Vls.Shopflow.Orders.Application.DataTransferObjects;

public sealed record OrderCustomerDto(string FullName, string Email, string Phone);

public sealed record OrderAddressDto(
    string ZipCode,
    string Street,
    string Number,
    string? Complement,
    string Neighborhood,
    string City,
    string State);

public sealed record OrderItemDto(
    Guid SkuId,
    string ProductName,
    string SkuCode,
    int Quantity,
    decimal UnitPrice,
    decimal Subtotal);

public sealed record OrderDto(
    Guid OrderId,
    string? OrderNumber,
    Guid CheckoutSessionId,
    string Status,
    OrderCustomerDto Customer,
    OrderAddressDto Address,
    IReadOnlyList<OrderItemDto> Items,
    decimal Subtotal,
    decimal? Shipping,
    decimal Total,
    DateTimeOffset CreatedAt);

public sealed record CreateOrderFromCheckoutSessionRequest(Guid CheckoutSessionId);
