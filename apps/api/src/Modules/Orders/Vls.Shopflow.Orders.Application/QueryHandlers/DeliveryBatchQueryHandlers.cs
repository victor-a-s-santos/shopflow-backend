using Vls.Shopflow.BuildingBlocks.Application.Interfaces;
using Vls.Shopflow.Orders.Application.DataTransferObjects;
using Vls.Shopflow.Orders.Application.Interfaces;
using Vls.Shopflow.Orders.Application.Mappers;
using Vls.Shopflow.Orders.Application.Queries;
using Vls.Shopflow.Orders.Application.Repositories;
using Vls.Shopflow.Orders.Application.Services;
using Vls.Shopflow.Orders.Domain.Enums;
using Vls.Shopflow.Orders.Domain.Exceptions;
using Vls.Shopflow.Orders.Domain.Services;

namespace Vls.Shopflow.Orders.Application.QueryHandlers;

public sealed class GetDeliveryBatchCandidatesQueryHandler(
    IOrderRepository orderRepository,
    IDeliveryBatchRepository batchRepository)
    : IQueryHandler<GetDeliveryBatchCandidatesQuery, DeliveryBatchCandidatesDto>
{
    public async Task<DeliveryBatchCandidatesDto> Handle(
        GetDeliveryBatchCandidatesQuery query,
        CancellationToken cancellationToken)
    {
        var baseOrder = await orderRepository.GetByIdWithItemsAsync(query.OrderId, cancellationToken)
                        ?? throw new OrderNotFoundException(query.OrderId);

        var identity = DeliveryBatchGroupingRules.ResolveIdentity([baseOrder]);

        var candidates = await orderRepository.FindEligibleGroupingCandidatesAsync(
            identity.CustomerUserId,
            identity.EmailNormalized,
            identity.PhoneNormalized,
            cancellationToken);

        var candidateIds = candidates.Select(c => c.Id).ToList();
        var inBatch = await batchRepository.GetOrderIdsInAnyBatchAsync(candidateIds, cancellationToken);

        var eligible = candidates
            .Where(o => DeliveryBatchGroupingRules.IsEligibleCandidate(o, inBatch.Contains(o.Id)))
            .OrderBy(o => o.CreatedAt)
            .ToList();

        // Include base if eligible but missing from query result (shouldn't happen).
        if (DeliveryBatchGroupingRules.IsEligibleCandidate(
                baseOrder,
                await batchRepository.IsOrderInAnyBatchAsync(baseOrder.Id, cancellationToken))
            && eligible.All(o => o.Id != baseOrder.Id))
        {
            eligible.Insert(0, baseOrder);
        }

        var addresses = DeliveryBatchGroupingRules.BuildAddressInfos(eligible);
        var hasDifferent = DeliveryBatchGroupingRules.HasDifferentAddresses(addresses);

        return new DeliveryBatchCandidatesDto(
            baseOrder.Id,
            DeliveryBatchMapper.ToCustomerDto(identity),
            hasDifferent,
            eligible.Select(o => new DeliveryBatchCandidateOrderDto(
                o.Id,
                o.FormatOrderNumber(),
                o.CreatedAt,
                o.Total,
                o.FulfillmentStatus.ToString(),
                o.PreferredDeliveryMethod?.ToString(),
                o.PreferredDeliveryDate,
                CustomerContactNormalizer.AddressSummary(
                    o.ShippingCity,
                    o.ShippingState,
                    o.ShippingZipCode))).ToList());
    }
}

public sealed class GetDeliveryBatchesQueryHandler(IDeliveryBatchReadModel readModel)
    : IQueryHandler<GetDeliveryBatchesQuery, PagedDeliveryBatchesDto>
{
    public async Task<PagedDeliveryBatchesDto> Handle(
        GetDeliveryBatchesQuery query,
        CancellationToken cancellationToken)
    {
        DeliveryBatchStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
            status = Enum.Parse<DeliveryBatchStatus>(query.Status.Trim(), ignoreCase: true);

        long? searchBatchNumber = null;
        long? searchOrderNumber = null;
        string? searchText = null;
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var raw = query.Q.Trim();
            var forceNumber = raw.StartsWith('#');
            var q = forceNumber ? raw.TrimStart('#') : raw;

            if (long.TryParse(q, out var number) && number > 0 && (forceNumber || q.Length <= 9))
            {
                // Ambiguous: try as batch number first in read model via SearchBatchNumber;
                // also pass as order number search.
                searchBatchNumber = number;
                searchOrderNumber = number;
            }
            else
            {
                searchText = raw.TrimStart('#');
            }
        }

        var sortAsc = string.Equals(query.Sort?.Trim(), "createdAt_asc", StringComparison.OrdinalIgnoreCase);

        var page = await readModel.GetPagedAsync(
            new DeliveryBatchListQuerySpec(
                query.Page,
                query.PageSize,
                status,
                searchText,
                searchBatchNumber,
                searchOrderNumber,
                query.CustomerEmail,
                query.CreatedFrom,
                query.CreatedTo,
                sortAsc),
            cancellationToken);

        var items = page.Items.Select(row => new DeliveryBatchListItemDto(
            row.Id,
            row.BatchNumber.ToString(),
            row.Status.ToString(),
            row.CustomerName,
            row.CustomerEmail,
            row.CustomerPhone,
            row.OrderCount,
            row.TotalAmount,
            row.DeliveryMethod?.ToString(),
            row.TrackingCode,
            row.CreatedAt,
            row.ShippedAt,
            row.DeliveredAt,
            row.HasDifferentDeliveryAddresses)).ToList();

        var totalPages = page.TotalItems == 0
            ? 0
            : (int)Math.Ceiling(page.TotalItems / (double)query.PageSize);

        return new PagedDeliveryBatchesDto(items, query.Page, query.PageSize, page.TotalItems, totalPages);
    }
}

public sealed class GetDeliveryBatchByIdQueryHandler(
    IDeliveryBatchRepository batchRepository,
    IOrderRepository orderRepository,
    IAdminOrderPixPaymentReader pixPaymentReader)
    : IQueryHandler<GetDeliveryBatchByIdQuery, DeliveryBatchDetailDto>
{
    public async Task<DeliveryBatchDetailDto> Handle(
        GetDeliveryBatchByIdQuery query,
        CancellationToken cancellationToken)
    {
        var batch = await batchRepository.GetByIdWithOrdersAsync(query.BatchId, cancellationToken)
                    ?? throw new DeliveryBatchNotFoundException(query.BatchId);

        var orderIds = batch.Orders.Select(o => o.OrderId).ToList();
        var orders = await orderRepository.GetByIdsWithItemsAsync(orderIds, cancellationToken);
        var payments = await pixPaymentReader.GetLatestByOrderIdsAsync(orderIds, cancellationToken);
        var paymentStatuses = payments.ToDictionary(kv => kv.Key, kv => (string?)kv.Value.Status);
        return DeliveryBatchMapper.ToDetailDto(batch, orders, paymentStatuses);
    }
}
