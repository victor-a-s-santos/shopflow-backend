using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.Orders.Application.Repositories;
using Vls.Shopflow.Orders.Domain.Enums;

namespace Vls.Shopflow.Orders.Infrastructure.Repositories;

public sealed class AdminOrderReadModel(OrdersDbContext db) : IAdminOrderReadModel
{
    public async Task<AdminOrderListPage> GetPagedAsync(
        AdminOrderListQuerySpec spec,
        CancellationToken cancellationToken)
    {
        var query = db.Orders.AsNoTracking().AsQueryable();

        if (spec.Status is { } status)
            query = query.Where(o => o.Status == status);

        if (spec.FulfillmentStatus is { } fulfillmentStatus)
            query = query.Where(o => o.FulfillmentStatus == fulfillmentStatus);

        if (spec.CreatedFrom is { } from)
            query = query.Where(o => o.CreatedAt >= from);

        if (spec.CreatedTo is { } to)
            query = query.Where(o => o.CreatedAt <= to);

        if (spec.PaidOnly == true)
            query = query.Where(o => o.Status == OrderStatus.Paid || o.PaidAt != null);
        else if (spec.PaidOnly == false)
            query = query.Where(o => o.Status != OrderStatus.Paid && o.PaidAt == null);

        if (spec.RestrictToOrderIds is { Count: > 0 } ids)
            query = query.Where(o => ids.Contains(o.Id));

        if (spec.SearchOrderId is { } orderId)
            query = query.Where(o => o.Id == orderId);
        else if (spec.SearchOrderNumber is { } orderNumber)
            query = query.Where(o => o.OrderNumber == orderNumber);
        else if (!string.IsNullOrWhiteSpace(spec.SearchText))
        {
            var term = spec.SearchText.Trim().ToLower();
            query = query.Where(o =>
                o.CustomerEmail.ToLower().Contains(term)
                || o.CustomerFullName.ToLower().Contains(term)
                || o.CustomerPhone.ToLower().Contains(term)
                || o.OrderNumber.ToString().Contains(term));
        }

        var totalItems = await query.CountAsync(cancellationToken);

        var ordered = spec.SortCreatedAtAscending
            ? query.OrderBy(o => o.CreatedAt)
            : query.OrderByDescending(o => o.CreatedAt);

        var pageItems = await ordered
            .Skip((spec.Page - 1) * spec.PageSize)
            .Take(spec.PageSize)
            .Select(o => new AdminOrderListRow(
                o.Id,
                o.OrderNumber,
                o.Status,
                o.CustomerFullName,
                o.CustomerEmail,
                o.CustomerPhone,
                o.Subtotal,
                o.ShippingAmount,
                o.Total,
                o.CreatedAt,
                o.PaidAt,
                o.Items.Count,
                o.FulfillmentStatus,
                o.PreferredDeliveryMethod,
                o.PreferredDeliveryDate,
                o.ShippedAt,
                o.DeliveredAt,
                o.TrackingCode))
            .ToListAsync(cancellationToken);

        return new AdminOrderListPage(pageItems, totalItems);
    }
}
