namespace Vls.Shopflow.Orders.Application.DataTransferObjects;

/// <summary>
/// Customer-facing Pix summary. No provider IDs, QR, copy-paste, or secrets.
/// </summary>
public sealed record CustomerOrderPaymentSummaryDto(
    string Status,
    string Provider,
    DateTimeOffset? PaidAt,
    DateTimeOffset? ExpiresAt);

public sealed record CustomerOrderListItemDto(
    Guid Id,
    string OrderNumber,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PaidAt,
    decimal Subtotal,
    decimal? ShippingAmount,
    decimal Total,
    int ItemsCount,
    string? FirstItemName,
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
    decimal Subtotal);

public sealed record CustomerOrderDetailDto(
    Guid Id,
    string OrderNumber,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? PaidAt,
    CustomerOrderShippingAddressDto ShippingAddress,
    CustomerOrderAmountsDto Amounts,
    IReadOnlyList<CustomerOrderItemDto> Items,
    CustomerOrderPaymentSummaryDto? Payment);
