using Vls.Shopflow.Orders.Domain.Enums;

namespace Vls.Shopflow.Orders.Application.Repositories;

public sealed record CustomerOrderListQuerySpec(
    Guid CustomerUserId,
    int Page,
    int PageSize,
    OrderStatus? Status,
    DateTimeOffset? CreatedFrom,
    DateTimeOffset? CreatedTo,
    IReadOnlyList<Guid>? RestrictToOrderIds,
    bool SortCreatedAtAscending = false);

public sealed record CustomerOrderListRow(
    Guid Id,
    OrderStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PaidAt,
    decimal Subtotal,
    decimal? ShippingAmount,
    decimal Total,
    int ItemsCount,
    string? FirstItemName);

public sealed record CustomerOrderListPage(
    IReadOnlyList<CustomerOrderListRow> Items,
    int TotalItems);

public interface ICustomerOrderReadModel
{
    Task<CustomerOrderListPage> GetPagedAsync(
        CustomerOrderListQuerySpec spec,
        CancellationToken cancellationToken);
}
