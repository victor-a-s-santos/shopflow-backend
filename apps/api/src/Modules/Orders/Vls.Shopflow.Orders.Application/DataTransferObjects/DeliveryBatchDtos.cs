namespace Vls.Shopflow.Orders.Application.DataTransferObjects;

public sealed record DeliveryBatchCustomerDto(
    Guid? CustomerUserId,
    string? Name,
    string? Email,
    string? Phone);

public sealed record DeliveryBatchOrderSummaryDto(
    Guid OrderId,
    string OrderNumber,
    DateTimeOffset CreatedAt,
    decimal Total,
    string Status,
    string? PaymentStatus,
    string FulfillmentStatus,
    string? PreferredDeliveryMethod,
    DateOnly? PreferredDeliveryDate,
    string? CustomerOrderNote,
    string AddressSummary);

public sealed record DeliveryBatchCandidateOrderDto(
    Guid OrderId,
    string OrderNumber,
    DateTimeOffset CreatedAt,
    decimal Total,
    string FulfillmentStatus,
    string? PreferredDeliveryMethod,
    DateOnly? PreferredDeliveryDate,
    string AddressSummary);

public sealed record DeliveryBatchCandidatesDto(
    Guid BaseOrderId,
    DeliveryBatchCustomerDto Customer,
    bool HasDifferentDeliveryAddresses,
    IReadOnlyList<DeliveryBatchCandidateOrderDto> Orders);

public sealed record DeliveryBatchListItemDto(
    Guid Id,
    string BatchNumber,
    string Status,
    string? CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    int OrderCount,
    decimal TotalAmount,
    string? DeliveryMethod,
    string? TrackingCode,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ShippedAt,
    DateTimeOffset? DeliveredAt,
    bool HasDifferentDeliveryAddresses);

public sealed record PagedDeliveryBatchesDto(
    IReadOnlyList<DeliveryBatchListItemDto> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);

public sealed record DeliveryBatchDetailDto(
    Guid Id,
    string BatchNumber,
    string Status,
    DeliveryBatchCustomerDto Customer,
    int OrderCount,
    decimal TotalAmount,
    string? DeliveryMethod,
    string? TrackingCode,
    string? InternalNote,
    DateTimeOffset CreatedAt,
    Guid? CreatedByAdminId,
    DateTimeOffset? UpdatedAt,
    Guid? UpdatedByAdminId,
    DateTimeOffset? ShippedAt,
    DateTimeOffset? DeliveredAt,
    bool HasDifferentDeliveryAddresses,
    IReadOnlyList<DeliveryBatchOrderSummaryDto> Orders);
