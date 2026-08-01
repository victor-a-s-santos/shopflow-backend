using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.Orders.Application.Repositories;
using Vls.Shopflow.Orders.Domain.Entities;
using Vls.Shopflow.Orders.Domain.Services;

namespace Vls.Shopflow.Orders.Infrastructure.Repositories;

public sealed class DeliveryBatchRepository(OrdersDbContext db) : IDeliveryBatchRepository
{
    public async Task AddAsync(DeliveryBatch batch, CancellationToken cancellationToken)
        => await db.DeliveryBatches.AddAsync(batch, cancellationToken);

    public Task<DeliveryBatch?> GetByIdWithOrdersAsync(Guid batchId, CancellationToken cancellationToken)
        => db.DeliveryBatches
            .Include(b => b.Orders)
            .FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken);

    public Task<bool> IsOrderInAnyBatchAsync(Guid orderId, CancellationToken cancellationToken)
        => db.DeliveryBatchOrders.AnyAsync(x => x.OrderId == orderId, cancellationToken);

    public async Task<IReadOnlySet<Guid>> GetOrderIdsInAnyBatchAsync(
        IReadOnlyCollection<Guid> orderIds,
        CancellationToken cancellationToken)
    {
        if (orderIds.Count == 0)
            return new HashSet<Guid>();

        var found = await db.DeliveryBatchOrders
            .AsNoTracking()
            .Where(x => orderIds.Contains(x.OrderId))
            .Select(x => x.OrderId)
            .ToListAsync(cancellationToken);

        return found.ToHashSet();
    }

    public async Task<DeliveryBatchMembership?> FindMembershipByOrderIdAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var row = await (
            from link in db.DeliveryBatchOrders.AsNoTracking()
            join batch in db.DeliveryBatches.AsNoTracking() on link.DeliveryBatchId equals batch.Id
            where link.OrderId == orderId
            select new { link.OrderId, BatchId = batch.Id, batch.BatchNumber }
        ).FirstOrDefaultAsync(cancellationToken);

        return row is null
            ? null
            : new DeliveryBatchMembership(row.OrderId, row.BatchId, row.BatchNumber);
    }

    public async Task<IReadOnlyDictionary<Guid, DeliveryBatchMembership>> FindMembershipsByOrderIdsAsync(
        IReadOnlyCollection<Guid> orderIds,
        CancellationToken cancellationToken)
    {
        if (orderIds.Count == 0)
            return new Dictionary<Guid, DeliveryBatchMembership>();

        var rows = await (
            from link in db.DeliveryBatchOrders.AsNoTracking()
            join batch in db.DeliveryBatches.AsNoTracking() on link.DeliveryBatchId equals batch.Id
            where orderIds.Contains(link.OrderId)
            select new { link.OrderId, BatchId = batch.Id, batch.BatchNumber }
        ).ToListAsync(cancellationToken);

        return rows.ToDictionary(
            r => r.OrderId,
            r => new DeliveryBatchMembership(r.OrderId, r.BatchId, r.BatchNumber));
    }
}

public sealed class DeliveryBatchReadModel(OrdersDbContext db) : IDeliveryBatchReadModel
{
    public async Task<DeliveryBatchListPage> GetPagedAsync(
        DeliveryBatchListQuerySpec spec,
        CancellationToken cancellationToken)
    {
        var query = db.DeliveryBatches.AsNoTracking().AsQueryable();

        if (spec.Status is { } status)
            query = query.Where(b => b.Status == status);

        if (spec.CreatedFrom is { } from)
            query = query.Where(b => b.CreatedAt >= from);

        if (spec.CreatedTo is { } to)
            query = query.Where(b => b.CreatedAt <= to);

        if (!string.IsNullOrWhiteSpace(spec.CustomerEmail))
        {
            var email = CustomerContactNormalizer.NormalizeEmail(spec.CustomerEmail);
            if (email is not null)
                query = query.Where(b => b.CustomerEmailNormalized == email);
        }

        if (spec.SearchBatchNumber is { } batchNumber || spec.SearchOrderNumber is { } || !string.IsNullOrWhiteSpace(spec.SearchText))
        {
            IQueryable<Guid>? byOrderNumber = null;
            if (spec.SearchOrderNumber is { } orderNumber)
            {
                byOrderNumber =
                    from link in db.DeliveryBatchOrders.AsNoTracking()
                    join o in db.Orders.AsNoTracking() on link.OrderId equals o.Id
                    where o.OrderNumber == orderNumber
                    select link.DeliveryBatchId;
            }

            if (spec.SearchBatchNumber is { } bn && byOrderNumber is not null)
            {
                query = query.Where(b => b.BatchNumber == bn || byOrderNumber.Contains(b.Id));
            }
            else if (spec.SearchBatchNumber is { } bnOnly)
            {
                query = query.Where(b => b.BatchNumber == bnOnly);
            }
            else if (byOrderNumber is not null)
            {
                query = query.Where(b => byOrderNumber.Contains(b.Id));
            }
            else if (!string.IsNullOrWhiteSpace(spec.SearchText))
            {
                var term = spec.SearchText.Trim().ToLower();
                query = query.Where(b =>
                    (b.CustomerName != null && b.CustomerName.ToLower().Contains(term))
                    || (b.CustomerEmail != null && b.CustomerEmail.ToLower().Contains(term))
                    || (b.CustomerPhone != null && b.CustomerPhone.ToLower().Contains(term))
                    || b.BatchNumber.ToString().Contains(term));
            }
        }

        var totalItems = await query.CountAsync(cancellationToken);

        var ordered = spec.SortCreatedAtAscending
            ? query.OrderBy(b => b.CreatedAt)
            : query.OrderByDescending(b => b.CreatedAt);

        var pageBatches = await ordered
            .Skip((spec.Page - 1) * spec.PageSize)
            .Take(spec.PageSize)
            .ToListAsync(cancellationToken);

        if (pageBatches.Count == 0)
            return new DeliveryBatchListPage([], totalItems);

        var batchIds = pageBatches.Select(b => b.Id).ToList();
        var orderLinks = await db.DeliveryBatchOrders.AsNoTracking()
            .Where(l => batchIds.Contains(l.DeliveryBatchId))
            .ToListAsync(cancellationToken);

        var orderIds = orderLinks.Select(l => l.OrderId).Distinct().ToList();
        var orderTotals = await db.Orders.AsNoTracking()
            .Where(o => orderIds.Contains(o.Id))
            .Select(o => new { o.Id, o.Total })
            .ToDictionaryAsync(o => o.Id, o => o.Total, cancellationToken);

        var totalsByBatch = orderLinks
            .GroupBy(l => l.DeliveryBatchId)
            .ToDictionary(
                g => g.Key,
                g => (
                    Count: g.Count(),
                    Total: g.Sum(l => orderTotals.TryGetValue(l.OrderId, out var t) ? t : 0m)));

        var items = pageBatches.Select(b =>
        {
            totalsByBatch.TryGetValue(b.Id, out var totals);
            return new DeliveryBatchListRow(
                b.Id,
                b.BatchNumber,
                b.Status,
                b.CustomerName,
                b.CustomerEmail,
                b.CustomerPhone,
                totals.Count,
                totals.Total,
                b.DeliveryMethod,
                b.TrackingCode,
                b.CreatedAt,
                b.ShippedAt,
                b.DeliveredAt,
                b.HasDifferentDeliveryAddresses);
        }).ToList();

        return new DeliveryBatchListPage(items, totalItems);
    }
}
