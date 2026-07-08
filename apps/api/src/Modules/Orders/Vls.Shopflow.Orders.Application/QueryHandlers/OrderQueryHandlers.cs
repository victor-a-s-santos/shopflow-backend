using MediatR;
using Vls.Shopflow.Orders.Application.Commands;
using Vls.Shopflow.Orders.Application.DataTransferObjects;
using Vls.Shopflow.Orders.Application.Mappers;
using Vls.Shopflow.Orders.Application.Repositories;
using Vls.Shopflow.Orders.Domain.Exceptions;

namespace Vls.Shopflow.Orders.Application.QueryHandlers;

public sealed class GetOrderByIdQueryHandler(IOrderRepository orderRepository)
    : IRequestHandler<GetOrderByIdQuery, OrderDto>
{
    public async Task<OrderDto> Handle(GetOrderByIdQuery query, CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByIdWithItemsAsync(query.OrderId, cancellationToken)
                    ?? throw new OrderNotFoundException(query.OrderId);

        return OrderMapper.ToDto(order);
    }
}

public sealed class GetOrderByCheckoutSessionIdQueryHandler(IOrderRepository orderRepository)
    : IRequestHandler<GetOrderByCheckoutSessionIdQuery, OrderDto>
{
    public async Task<OrderDto> Handle(
        GetOrderByCheckoutSessionIdQuery query,
        CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByCheckoutSessionIdWithItemsAsync(
                        query.CheckoutSessionId,
                        cancellationToken)
                    ?? throw new OrderNotFoundByCheckoutSessionException(query.CheckoutSessionId);

        return OrderMapper.ToDto(order);
    }
}
