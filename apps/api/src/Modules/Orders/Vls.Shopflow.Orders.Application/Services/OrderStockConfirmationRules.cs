using Vls.Shopflow.Orders.Domain.Entities;
using Vls.Shopflow.Orders.Domain.Enums;
using Vls.Shopflow.Orders.Domain.Exceptions;

namespace Vls.Shopflow.Orders.Application.Services;

public static class OrderStockConfirmationRules
{
    public static bool CanConfirmStock(Order order)
        => order.Status == OrderStatus.Paid
           && order.FulfillmentStatus != FulfillmentStatus.Delivered
           && order.StockConfirmedAt is null;

    public static bool CanMarkAsSeparated(Order order, bool requireStockConfirmation)
        => order.Status == OrderStatus.Paid
           && order.FulfillmentStatus == FulfillmentStatus.AwaitingShipment
           && (!requireStockConfirmation || order.StockConfirmedAt is not null);

    public static void EnsureConfirmedForShipment(Order order, bool requireStockConfirmation)
    {
        if (requireStockConfirmation && order.StockConfirmedAt is null)
            throw new OrderStockConfirmationRequiredException(order.Id);
    }
}
