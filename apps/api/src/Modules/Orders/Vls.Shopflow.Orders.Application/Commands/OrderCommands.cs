using Vls.Shopflow.BuildingBlocks.Application.Interfaces;
using Vls.Shopflow.Orders.Application.DataTransferObjects;

namespace Vls.Shopflow.Orders.Application.Commands;

public sealed record CreateOrderFromCheckoutSessionCommand(Guid CheckoutSessionId)
    : ICommand<OrderDto>;

public sealed record GetOrderByIdQuery(Guid OrderId)
    : IQuery<OrderDto>;

public sealed record GetOrderByCheckoutSessionIdQuery(Guid CheckoutSessionId)
    : IQuery<OrderDto>;
