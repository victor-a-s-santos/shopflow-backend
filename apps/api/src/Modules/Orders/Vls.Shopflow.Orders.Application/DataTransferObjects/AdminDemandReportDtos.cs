namespace Vls.Shopflow.Orders.Application.DataTransferObjects;

public sealed record AdminProductDemandItemDto(
    Guid SkuId,
    string ProductName,
    string SkuCode,
    int TotalQuantity,
    int OrderCount,
    decimal SubtotalTotal,
    OrderItemSalesDisplayDto? SalesDisplay);

public sealed record PagedAdminProductDemandDto(
    IReadOnlyList<AdminProductDemandItemDto> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);

public sealed record AdminProductDemandOrderDto(
    Guid OrderId,
    string OrderNumber,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PaidAt,
    string Status,
    bool IsGuest,
    string CustomerFullName,
    string CustomerEmail,
    string CustomerPhone,
    string? PhoneDigits,
    Guid SkuId,
    string ProductName,
    string SkuCode,
    int Quantity,
    decimal? UnitPrice,
    decimal Subtotal,
    OrderItemSalesDisplayDto? SalesDisplay,
    AdminOrderPaymentSummaryDto? Payment);

public sealed record PagedAdminProductDemandOrdersDto(
    IReadOnlyList<AdminProductDemandOrderDto> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);

public sealed record AdminUnpaidOrderItemDto(
    Guid Id,
    Guid SkuId,
    string SkuCode,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal Subtotal,
    OrderItemSalesDisplayDto? SalesDisplay);

public sealed record AdminUnpaidOrderDto(
    Guid OrderId,
    string OrderNumber,
    DateTimeOffset CreatedAt,
    string Status,
    bool IsGuest,
    string CustomerFullName,
    string CustomerEmail,
    string CustomerPhone,
    string? PhoneDigits,
    decimal Subtotal,
    decimal? ShippingAmount,
    decimal Total,
    AdminOrderPaymentSummaryDto? Payment,
    IReadOnlyList<AdminUnpaidOrderItemDto> Items);

public sealed record PagedAdminUnpaidOrdersDto(
    IReadOnlyList<AdminUnpaidOrderDto> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);
