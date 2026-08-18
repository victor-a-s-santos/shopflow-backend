using Vls.Shopflow.Orders.Application.Interfaces;
using Vls.Shopflow.Orders.Domain.Entities;

namespace Vls.Shopflow.Orders.Application.Mappers;

public static class OrderEmailNotifyMapper
{
    public static OrderEmailNotifyRequest FromOrder(
        Order order,
        string? guestAccessToken = null)
        => new(
            order.Id,
            order.OrderNumber,
            order.CustomerEmail,
            order.CustomerFullName,
            order.Total,
            order.CustomerUserId,
            guestAccessToken,
            order.TrackingCode,
            order.FinalDeliveryMethod?.ToString(),
            order.PreferredDeliveryMethod?.ToString(),
            order.PreferredDeliveryDate);
}
