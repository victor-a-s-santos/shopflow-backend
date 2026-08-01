using Vls.Shopflow.Orders.Application.DataTransferObjects;
using Vls.Shopflow.Orders.Domain.Entities;

namespace Vls.Shopflow.Orders.Application.Mappers;

internal static class AdminOrderMapper
{
    public static AdminOrderDetailDto ToDetailDto(
        Order order,
        AdminOrderPaymentSummaryDto? payment,
        Guid? deliveryBatchId = null,
        string? deliveryBatchNumber = null)
        => new(
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
                    i.Subtotal,
                    OrderItemSalesDisplayMapper.ToDto(i)))
                .ToList(),
            payment,
            order.PreferredDeliveryMethod?.ToString(),
            order.PreferredDeliveryDate,
            order.CustomerOrderNote,
            order.InternalOrderNote,
            order.FulfillmentStatus.ToString(),
            order.FinalDeliveryMethod?.ToString(),
            order.TrackingCode,
            order.ShippedAt,
            order.DeliveredAt,
            order.FulfillmentUpdatedAt,
            order.FulfillmentUpdatedByAdminId,
            deliveryBatchId,
            deliveryBatchNumber);

    public static OrderDeliveryInfoDto ToSafeDeliveryDto(Order order)
        => new(
            order.PreferredDeliveryMethod?.ToString(),
            order.PreferredDeliveryDate,
            order.CustomerOrderNote,
            order.FulfillmentStatus.ToString(),
            order.FinalDeliveryMethod?.ToString(),
            order.TrackingCode,
            order.ShippedAt,
            order.DeliveredAt);
}
