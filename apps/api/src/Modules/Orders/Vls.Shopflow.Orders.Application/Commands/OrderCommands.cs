using Vls.Shopflow.BuildingBlocks.Application.Interfaces;
using Vls.Shopflow.Orders.Application.DataTransferObjects;

namespace Vls.Shopflow.Orders.Application.Commands;

public sealed record CreateOrderFromCheckoutSessionCommand(
    Guid CheckoutSessionId,
    Guid? CustomerUserId = null)
    : ICommand<OrderDto>;

public sealed record GetOrderByIdQuery(Guid OrderId)
    : IQuery<OrderDto>;

public sealed record GetOrderByCheckoutSessionIdQuery(Guid CheckoutSessionId)
    : IQuery<OrderDto>;

public sealed record GetGuestOrderStatusQuery(Guid OrderId, string? AccessToken)
    : IQuery<GuestOrderStatusDto>;

/// <summary>
/// Public guest tracking by friendly order number + access token (not GUID alone).
/// </summary>
public sealed record GetPublicOrderStatusQuery(string OrderNumber, string? AccessToken)
    : IQuery<GuestOrderStatusDto>;
