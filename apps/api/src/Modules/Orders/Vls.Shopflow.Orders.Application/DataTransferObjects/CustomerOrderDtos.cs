namespace Vls.Shopflow.Orders.Application.DataTransferObjects;

/// <summary>
/// Customer-facing Pix summary. No provider tech names, QR, copy-paste, or secrets.
/// <see cref="ExpiresAt"/> is set only while payment is Pending.
/// </summary>
public sealed record CustomerOrderPaymentSummaryDto(
    string Status,
    string Method,
    DateTimeOffset? PaidAt,
    DateTimeOffset? ExpiresAt);

public sealed record CustomerOrderListItemDto(
    Guid Id,
    string OrderNumber,
    string CustomerStatus,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PaidAt,
    decimal Subtotal,
    decimal? ShippingAmount,
    decimal Total,
    string Currency,
    int ItemsCount,
    string? FirstItemName,
    string? PreviewImageUrl,
    CustomerOrderPaymentSummaryDto? Payment);

public sealed record PagedCustomerOrdersDto(
    IReadOnlyList<CustomerOrderListItemDto> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);

public sealed record CustomerOrderShippingAddressDto(
    string Street,
    string Number,
    string? Complement,
    string Neighborhood,
    string City,
    string State,
    string ZipCode);

public sealed record CustomerOrderAmountsDto(
    decimal Subtotal,
    decimal? ShippingAmount,
    decimal Total);

public sealed record CustomerOrderItemDto(
    Guid Id,
    Guid SkuId,
    string SkuCode,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal Subtotal,
    OrderItemSalesDisplayDto? SalesDisplay = null);

public sealed record CustomerOrderDetailDto(
    Guid Id,
    string OrderNumber,
    string CustomerStatus,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? PaidAt,
    CustomerOrderShippingAddressDto ShippingAddress,
    CustomerOrderAmountsDto Amounts,
    string Currency,
    IReadOnlyList<CustomerOrderItemDto> Items,
    CustomerOrderPaymentSummaryDto? Payment);
