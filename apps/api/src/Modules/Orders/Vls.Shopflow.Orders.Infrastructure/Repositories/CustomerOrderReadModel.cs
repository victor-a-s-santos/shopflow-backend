using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.Orders.Application.Repositories;
using Vls.Shopflow.Orders.Domain.Enums;

namespace Vls.Shopflow.Orders.Infrastructure.Repositories;

public sealed class CustomerOrderReadModel(OrdersDbContext db) : ICustomerOrderReadModel
{
    public async Task<CustomerOrderListPage> GetPagedAsync(
        CustomerOrderListQuerySpec spec,
        CancellationToken cancellationToken)
    {
        // Strict ownership: never filter by email.
        var query = db.Orders.AsNoTracking()
            .Where(o => o.CustomerUserId == spec.CustomerUserId);

        if (spec.Status is { } status)
            query = query.Where(o => o.Status == status);

        if (spec.CreatedFrom is { } from)
            query = query.Where(o => o.CreatedAt >= from);

        if (spec.CreatedTo is { } to)
            query = query.Where(o => o.CreatedAt <= to);

        if (spec.RestrictToOrderIds is { Count: > 0 } ids)
            query = query.Where(o => ids.Contains(o.Id));

        var totalItems = await query.CountAsync(cancellationToken);

        var ordered = spec.SortCreatedAtAscending
            ? query.OrderBy(o => o.CreatedAt)
            : query.OrderByDescending(o => o.CreatedAt);

        var pageItems = await ordered
            .Skip((spec.Page - 1) * spec.PageSize)
            .Take(spec.PageSize)
            .Select(o => new CustomerOrderListRow(
                o.Id,
                o.OrderNumber,
                o.Status,
                o.CreatedAt,
                o.PaidAt,
                o.Subtotal,
                o.ShippingAmount,
                o.Total,
                o.Items.Count,
                o.Items
                    .OrderBy(i => i.ProductName)
                    .Select(i => i.ProductName)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);

        return new CustomerOrderListPage(pageItems, totalItems);
    }
}
