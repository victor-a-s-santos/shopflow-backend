using Vls.Shopflow.BuildingBlocks.Application.Interfaces;
using Vls.Shopflow.Orders.Application.DataTransferObjects;
using Vls.Shopflow.Orders.Application.Interfaces;
using Vls.Shopflow.Orders.Application.Mappers;
using Vls.Shopflow.Orders.Application.Queries;
using Vls.Shopflow.Orders.Application.Repositories;
using Vls.Shopflow.Orders.Application.Services;
using Vls.Shopflow.Orders.Domain.Enums;
using Vls.Shopflow.Orders.Domain.Exceptions;

namespace Vls.Shopflow.Orders.Application.QueryHandlers;

public sealed class GetCustomerOrdersQueryHandler(
    ICustomerOrderReadModel readModel,
    ICustomerOrderPixPaymentReader pixPaymentReader)
    : IQueryHandler<GetCustomerOrdersQuery, PagedCustomerOrdersDto>
{
    public const string DefaultCurrency = "BRL";

    public async Task<PagedCustomerOrdersDto> Handle(
        GetCustomerOrdersQuery query,
        CancellationToken cancellationToken)
    {
        OrderStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status)
            && OrderCustomerStatusProjector.TryParseListFilter(query.Status, out var parsedStatus))
            status = parsedStatus;

        IReadOnlyList<Guid>? restrictToOrderIds = null;
        if (!string.IsNullOrWhiteSpace(query.PaymentStatus))
        {
            restrictToOrderIds = await pixPaymentReader.FindOrderIdsByLatestPaymentStatusAsync(
                query.PaymentStatus.Trim(),
                cancellationToken);

            if (restrictToOrderIds.Count == 0)
                return new PagedCustomerOrdersDto([], query.Page, query.PageSize, 0, 0);
        }

        var sortAsc = string.Equals(query.Sort?.Trim(), "createdAt_asc", StringComparison.OrdinalIgnoreCase);

        var page = await readModel.GetPagedAsync(
            new CustomerOrderListQuerySpec(
                query.CustomerUserId,
                query.Page,
                query.PageSize,
                status,
                query.CreatedFrom,
                query.CreatedTo,
                restrictToOrderIds,
                sortAsc),
            cancellationToken);

        var orderIds = page.Items.Select(x => x.Id).ToList();
        var payments = orderIds.Count == 0
            ? new Dictionary<Guid, CustomerOrderPaymentSummaryDto>()
            : await pixPaymentReader.GetLatestByOrderIdsAsync(orderIds, cancellationToken);

        var items = page.Items.Select(row =>
        {
            payments.TryGetValue(row.Id, out var payment);
            return new CustomerOrderListItemDto(
                row.Id,
                row.OrderNumber.ToString(),
                OrderCustomerStatusProjector.Project(row.Status, payment?.Status),
                row.Status.ToString(),
                row.CreatedAt,
                row.PaidAt,
                row.Subtotal,
                row.ShippingAmount,
                row.Total,
                DefaultCurrency,
                row.ItemsCount,
                row.FirstItemName,
                PreviewImageUrl: null,
                payment);
        }).ToList();

        var totalPages = page.TotalItems == 0
            ? 0
            : (int)Math.Ceiling(page.TotalItems / (double)query.PageSize);

        return new PagedCustomerOrdersDto(items, query.Page, query.PageSize, page.TotalItems, totalPages);
    }
}

public sealed class GetCustomerOrderByIdQueryHandler(
    IOrderRepository orderRepository,
    ICustomerOrderPixPaymentReader pixPaymentReader)
    : IQueryHandler<GetCustomerOrderByIdQuery, CustomerOrderDetailDto>
{
    public async Task<CustomerOrderDetailDto> Handle(
        GetCustomerOrderByIdQuery query,
        CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByIdWithItemsAsync(query.OrderId, cancellationToken);

        // Hide existence of other customers' / guest orders.
        if (order is null || order.CustomerUserId != query.CustomerUserId)
            throw new OrderNotFoundException(query.OrderId);

        var payment = await pixPaymentReader.GetLatestByOrderIdAsync(order.Id, cancellationToken);

        return new CustomerOrderDetailDto(
            order.Id,
            order.FormatOrderNumber(),
            OrderCustomerStatusProjector.Project(order.Status, payment?.Status),
            order.Status.ToString(),
            order.CreatedAt,
            order.UpdatedAt,
            order.PaidAt,
            new CustomerOrderShippingAddressDto(
                order.ShippingStreet,
                order.ShippingNumber,
                order.ShippingComplement,
                order.ShippingNeighborhood,
                order.ShippingCity,
                order.ShippingState,
                order.ShippingZipCode),
            new CustomerOrderAmountsDto(order.Subtotal, order.ShippingAmount, order.Total),
            GetCustomerOrdersQueryHandler.DefaultCurrency,
            order.Items
                .OrderBy(i => i.ProductName)
                .ThenBy(i => i.SkuCode)
                .Select(i => new CustomerOrderItemDto(
                    i.Id,
                    i.SkuId,
                    i.SkuCode,
                    i.ProductName,
                    i.Quantity,
                    i.UnitPrice,
                    i.Subtotal,
                    OrderItemSalesDisplayMapper.ToDto(i)))
                .ToList(),
            payment);
    }
}
