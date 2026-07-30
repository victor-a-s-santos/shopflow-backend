namespace Vls.Shopflow.Orders.Application.DataTransferObjects;

/// <summary>
/// Safe Pix summary for Backoffice. Excludes QR, copy-paste, ticket URL, tokens and secrets.
/// Latest payment per order is chosen by CreatedAt desc when multiple exist.
/// </summary>
public sealed record AdminOrderPaymentSummaryDto(
    Guid Id,
    string Provider,
    string Status,
    string? ProviderOrderId,
    string? ProviderPaymentId,
    string? ProviderTransactionId,
    string? ProviderStatus,
    string? ProviderStatusDetail,
    string? ProviderTransactionStatus,
    string? ProviderTransactionStatusDetail,
    DateTimeOffset? ProviderApprovedAt,
    DateTimeOffset? ProviderUpdatedAt,
    DateTimeOffset? PaidAt,
    DateTimeOffset? ExpiresAt);

public sealed record AdminOrderListItemDto(
    Guid Id,
    string OrderNumber,
    string Status,
    string CustomerFullName,
    string CustomerEmail,
    string CustomerPhone,
    decimal Subtotal,
    decimal? ShippingAmount,
    decimal Total,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PaidAt,
    int ItemsCount,
    AdminOrderPaymentSummaryDto? Payment,
    string FulfillmentStatus,
    string? PreferredDeliveryMethod = null,
    DateOnly? PreferredDeliveryDate = null,
    DateTimeOffset? ShippedAt = null,
    DateTimeOffset? DeliveredAt = null,
    string? TrackingCode = null);

public sealed record PagedAdminOrdersDto(
    IReadOnlyList<AdminOrderListItemDto> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);

public sealed record AdminOrderCustomerDto(
    string FullName,
    string Email,
    string Phone);

public sealed record AdminOrderShippingAddressDto(
    string Street,
    string Number,
    string? Complement,
    string Neighborhood,
    string City,
    string State,
    string ZipCode);

public sealed record AdminOrderAmountsDto(
    decimal Subtotal,
    decimal? ShippingAmount,
    decimal Total);

public sealed record AdminOrderItemDto(
    Guid Id,
    Guid SkuId,
    string SkuCode,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal Subtotal,
    OrderItemSalesDisplayDto? SalesDisplay = null);

public sealed record AdminOrderDetailDto(
    Guid Id,
    string OrderNumber,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? PaidAt,
    AdminOrderCustomerDto Customer,
    AdminOrderShippingAddressDto ShippingAddress,
    AdminOrderAmountsDto Amounts,
    IReadOnlyList<AdminOrderItemDto> Items,
    AdminOrderPaymentSummaryDto? Payment,
    string? PreferredDeliveryMethod = null,
    DateOnly? PreferredDeliveryDate = null,
    string? CustomerOrderNote = null,
    string? InternalOrderNote = null,
    string FulfillmentStatus = "AwaitingShipment",
    string? FinalDeliveryMethod = null,
    string? TrackingCode = null,
    DateTimeOffset? ShippedAt = null,
    DateTimeOffset? DeliveredAt = null,
    DateTimeOffset? FulfillmentUpdatedAt = null,
    Guid? FulfillmentUpdatedByAdminId = null);

/// <summary>Safe delivery/fulfillment projection (no internal notes or admin ids).</summary>
public sealed record OrderDeliveryInfoDto(
    string? PreferredDeliveryMethod,
    DateOnly? PreferredDeliveryDate,
    string? CustomerOrderNote,
    string FulfillmentStatus,
    string? FinalDeliveryMethod,
    string? TrackingCode,
    DateTimeOffset? ShippedAt,
    DateTimeOffset? DeliveredAt);
