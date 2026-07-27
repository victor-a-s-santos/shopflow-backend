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
    decimal Subtotal,
    OrderItemSalesDisplayDto? SalesDisplay = null);

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
    DateTimeOffset CreatedAt,
    string? GuestAccessToken = null,
    DateTimeOffset? GuestAccessTokenExpiresAt = null);

public sealed record CreateOrderFromCheckoutSessionRequest(Guid CheckoutSessionId);

public sealed record GuestOrderMaskedCustomerDto(string Name, string Email);

public sealed record GuestOrderPaymentStatusDto(
    string Status,
    string Method,
    decimal? Amount,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? PaidAt,
    DateTimeOffset? UpdatedAt);

public sealed record GuestOrderItemStatusDto(
    string ProductName,
    Guid SkuId,
    int Quantity,
    decimal UnitPrice,
    decimal Total,
    IReadOnlyDictionary<string, string>? Attributes,
    string? ImageUrl,
    OrderItemSalesDisplayDto? SalesDisplay = null);

public sealed record GuestOrderTotalsDto(
    decimal Subtotal,
    decimal Discount,
    decimal? Shipping,
    decimal Total);

public sealed record GuestOrderAccessMetaDto(
    DateTimeOffset ExpiresAt,
    DateTimeOffset? LastUsedAt);

public sealed record GuestOrderStatusDto(
    Guid OrderId,
    string? OrderNumber,
    string CustomerStatus,
    string OrderStatus,
    GuestOrderPaymentStatusDto? Payment,
    IReadOnlyList<GuestOrderItemStatusDto> Items,
    GuestOrderTotalsDto Totals,
    GuestOrderMaskedCustomerDto Customer,
    GuestOrderAccessMetaDto Access,
    bool CanCreateAccount,
    bool AccountExistsForEmail);
