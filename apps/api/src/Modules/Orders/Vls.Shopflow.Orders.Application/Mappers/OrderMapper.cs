using Vls.Shopflow.Orders.Domain.Entities;

namespace Vls.Shopflow.Orders.Application.Mappers;

internal static class OrderMapper
{
    public static DataTransferObjects.OrderDto ToDto(Order order)
        => new(
            order.Id,
            order.Id.ToString(),
            order.CheckoutSessionId,
            order.Status.ToString(),
            new DataTransferObjects.OrderCustomerDto(
                order.CustomerFullName,
                order.CustomerEmail,
                order.CustomerPhone),
            new DataTransferObjects.OrderAddressDto(
                order.ShippingZipCode,
                order.ShippingStreet,
                order.ShippingNumber,
                order.ShippingComplement,
                order.ShippingNeighborhood,
                order.ShippingCity,
                order.ShippingState),
            order.Items.Select(i => new DataTransferObjects.OrderItemDto(
                i.SkuId,
                i.ProductName,
                i.SkuCode,
                i.Quantity,
                i.UnitPrice,
                i.Subtotal)).ToList(),
            order.Subtotal,
            order.ShippingAmount,
            order.Total,
            order.CreatedAt);

    public static void EnsureCheckoutSessionCanCreateOrder(string status, Guid checkoutSessionId)
    {
        if (!string.Equals(status, "Pending", StringComparison.Ordinal))
        {
            throw new Domain.Exceptions.InvalidCheckoutSessionForOrderException(
                checkoutSessionId,
                $"Checkout session {checkoutSessionId} cannot create an order because its status is {status}.");
        }
    }
}
