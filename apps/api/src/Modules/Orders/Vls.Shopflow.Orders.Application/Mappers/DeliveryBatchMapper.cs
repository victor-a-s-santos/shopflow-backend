using Vls.Shopflow.Orders.Application.DataTransferObjects;
using Vls.Shopflow.Orders.Application.Services;
using Vls.Shopflow.Orders.Domain.Entities;
using Vls.Shopflow.Orders.Domain.Services;

namespace Vls.Shopflow.Orders.Application.Mappers;

internal static class DeliveryBatchMapper
{
    public static DeliveryBatchCustomerDto ToCustomerDto(DeliveryBatchCustomerIdentity identity)
        => new(identity.CustomerUserId, identity.Name, identity.Email, identity.Phone);

    public static DeliveryBatchCustomerDto ToCustomerDto(DeliveryBatch batch)
        => new(batch.CustomerUserId, batch.CustomerName, batch.CustomerEmail, batch.CustomerPhone);

    public static DeliveryBatchDetailDto ToDetailDto(
        DeliveryBatch batch,
        IReadOnlyList<Order> orders,
        IReadOnlyDictionary<Guid, string?> paymentStatuses)
    {
        var orderDtos = orders
            .OrderBy(o => o.CreatedAt)
            .Select(o => new DeliveryBatchOrderSummaryDto(
                o.Id,
                o.FormatOrderNumber(),
                o.CreatedAt,
                o.Total,
                o.Status.ToString(),
                paymentStatuses.TryGetValue(o.Id, out var ps) ? ps : null,
                o.FulfillmentStatus.ToString(),
                o.PreferredDeliveryMethod?.ToString(),
                o.PreferredDeliveryDate,
                o.CustomerOrderNote,
                CustomerContactNormalizer.AddressSummary(o.ShippingCity, o.ShippingState, o.ShippingZipCode)))
            .ToList();

        return new DeliveryBatchDetailDto(
            batch.Id,
            batch.FormatBatchNumber(),
            batch.Status.ToString(),
            ToCustomerDto(batch),
            orderDtos.Count,
            orderDtos.Sum(o => o.Total),
            batch.DeliveryMethod?.ToString(),
            batch.TrackingCode,
            batch.InternalNote,
            batch.CreatedAt,
            batch.CreatedByAdminId,
            batch.UpdatedAt,
            batch.UpdatedByAdminId,
            batch.ShippedAt,
            batch.DeliveredAt,
            batch.HasDifferentDeliveryAddresses,
            orderDtos);
    }
}
