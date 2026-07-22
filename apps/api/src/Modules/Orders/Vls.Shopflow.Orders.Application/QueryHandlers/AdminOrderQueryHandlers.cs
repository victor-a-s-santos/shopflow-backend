using Vls.Shopflow.BuildingBlocks.Application.Interfaces;
using Vls.Shopflow.Orders.Application.DataTransferObjects;
using Vls.Shopflow.Orders.Application.Interfaces;
using Vls.Shopflow.Orders.Application.Queries;
using Vls.Shopflow.Orders.Application.Repositories;
using Vls.Shopflow.Orders.Domain.Enums;
using Vls.Shopflow.Orders.Domain.Exceptions;

namespace Vls.Shopflow.Orders.Application.QueryHandlers;

public sealed class GetAdminOrdersQueryHandler(
    IAdminOrderReadModel readModel,
    IAdminOrderPixPaymentReader pixPaymentReader)
    : IQueryHandler<GetAdminOrdersQuery, PagedAdminOrdersDto>
{
    public async Task<PagedAdminOrdersDto> Handle(
        GetAdminOrdersQuery query,
        CancellationToken cancellationToken)
    {
        OrderStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
            status = Enum.Parse<OrderStatus>(query.Status.Trim(), ignoreCase: true);

        IReadOnlyList<Guid>? restrictToOrderIds = null;
        if (!string.IsNullOrWhiteSpace(query.PaymentStatus))
        {
            restrictToOrderIds = await pixPaymentReader.FindOrderIdsByLatestPaymentStatusAsync(
                query.PaymentStatus.Trim(),
                cancellationToken);

            if (restrictToOrderIds.Count == 0)
            {
                return new PagedAdminOrdersDto([], query.Page, query.PageSize, 0, 0);
            }
        }

        Guid? searchOrderId = null;
        long? searchOrderNumber = null;
        string? searchText = null;
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var raw = query.Q.Trim();
            var forceOrderNumber = raw.StartsWith('#');
            var q = forceOrderNumber ? raw.TrimStart('#') : raw;

            if (Guid.TryParse(q, out var orderId))
                searchOrderId = orderId;
            // Avoid treating phone numbers (10–11 digits) as order numbers.
            else if (long.TryParse(q, out var orderNumber)
                     && orderNumber > 0
                     && (forceOrderNumber || q.Length <= 9))
                searchOrderNumber = orderNumber;
            else
                searchText = raw.TrimStart('#');
        }

        var sortAsc = string.Equals(query.Sort?.Trim(), "createdAt_asc", StringComparison.OrdinalIgnoreCase);

        var page = await readModel.GetPagedAsync(
            new AdminOrderListQuerySpec(
                query.Page,
                query.PageSize,
                status,
                query.CreatedFrom,
                query.CreatedTo,
                query.PaidOnly,
                searchText,
                searchOrderId,
                searchOrderNumber,
                restrictToOrderIds,
                sortAsc),
            cancellationToken);

        var orderIds = page.Items.Select(x => x.Id).ToList();
        var payments = orderIds.Count == 0
            ? new Dictionary<Guid, AdminOrderPaymentSummaryDto>()
            : await pixPaymentReader.GetLatestByOrderIdsAsync(orderIds, cancellationToken);

        var items = page.Items.Select(row => new AdminOrderListItemDto(
            row.Id,
            row.OrderNumber.ToString(),
            row.Status.ToString(),
            row.CustomerFullName,
            row.CustomerEmail,
            row.CustomerPhone,
            row.Subtotal,
            row.ShippingAmount,
            row.Total,
            row.CreatedAt,
            row.PaidAt,
            row.ItemsCount,
            payments.TryGetValue(row.Id, out var payment) ? payment : null)).ToList();

        var totalPages = page.TotalItems == 0
            ? 0
            : (int)Math.Ceiling(page.TotalItems / (double)query.PageSize);

        return new PagedAdminOrdersDto(items, query.Page, query.PageSize, page.TotalItems, totalPages);
    }
}

public sealed class GetAdminOrderByIdQueryHandler(
    IOrderRepository orderRepository,
    IAdminOrderPixPaymentReader pixPaymentReader)
    : IQueryHandler<GetAdminOrderByIdQuery, AdminOrderDetailDto>
{
    public async Task<AdminOrderDetailDto> Handle(
        GetAdminOrderByIdQuery query,
        CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByIdWithItemsAsync(query.OrderId, cancellationToken)
                    ?? throw new OrderNotFoundException(query.OrderId);

        var payment = await pixPaymentReader.GetLatestByOrderIdAsync(order.Id, cancellationToken);

        return new AdminOrderDetailDto(
            order.Id,
            order.FormatOrderNumber(),
            order.Status.ToString(),
            order.CreatedAt,
            order.UpdatedAt,
            order.PaidAt,
            new AdminOrderCustomerDto(order.CustomerFullName, order.CustomerEmail, order.CustomerPhone),
            new AdminOrderShippingAddressDto(
                order.ShippingStreet,
                order.ShippingNumber,
                order.ShippingComplement,
                order.ShippingNeighborhood,
                order.ShippingCity,
                order.ShippingState,
                order.ShippingZipCode),
            new AdminOrderAmountsDto(order.Subtotal, order.ShippingAmount, order.Total),
            order.Items
                .OrderBy(i => i.ProductName)
                .ThenBy(i => i.SkuCode)
                .Select(i => new AdminOrderItemDto(
                    i.Id,
                    i.SkuId,
                    i.SkuCode,
                    i.ProductName,
                    i.Quantity,
                    i.UnitPrice,
                    i.Subtotal))
                .ToList(),
            payment);
    }
}
