using Vls.Shopflow.Orders.Domain.Enums;

namespace Vls.Shopflow.Orders.Application.Repositories;

public sealed record AdminOrderListQuerySpec(
    int Page,
    int PageSize,
    OrderStatus? Status,
    DateTimeOffset? CreatedFrom,
    DateTimeOffset? CreatedTo,
    bool? PaidOnly,
    string? SearchText,
    Guid? SearchOrderId,
    long? SearchOrderNumber,
    IReadOnlyList<Guid>? RestrictToOrderIds,
    bool SortCreatedAtAscending = false,
    FulfillmentStatus? FulfillmentStatus = null);

public sealed record AdminOrderListRow(
    Guid Id,
    long OrderNumber,
    OrderStatus Status,
    string CustomerFullName,
    string CustomerEmail,
    string CustomerPhone,
    decimal Subtotal,
    decimal? ShippingAmount,
    decimal Total,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PaidAt,
    int ItemsCount,
    FulfillmentStatus FulfillmentStatus,
    DeliveryMethod? PreferredDeliveryMethod,
    DateOnly? PreferredDeliveryDate,
    DateTimeOffset? ShippedAt,
    DateTimeOffset? DeliveredAt,
    string? TrackingCode);

public sealed record AdminOrderListPage(
    IReadOnlyList<AdminOrderListRow> Items,
    int TotalItems);

public interface IAdminOrderReadModel
{
    Task<AdminOrderListPage> GetPagedAsync(
        AdminOrderListQuerySpec spec,
        CancellationToken cancellationToken);
}
