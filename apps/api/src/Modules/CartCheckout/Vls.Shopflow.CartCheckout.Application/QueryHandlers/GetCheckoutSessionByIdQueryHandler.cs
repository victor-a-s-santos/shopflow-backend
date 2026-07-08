using MediatR;
using Vls.Shopflow.CartCheckout.Application.DataTransferObjects;
using Vls.Shopflow.CartCheckout.Application.Mappers;
using Vls.Shopflow.CartCheckout.Application.Queries;
using Vls.Shopflow.CartCheckout.Application.Repositories;
using Vls.Shopflow.CartCheckout.Domain.Exceptions;

namespace Vls.Shopflow.CartCheckout.Application.QueryHandlers;

public sealed class GetCheckoutSessionByIdQueryHandler(
    ICheckoutSessionRepository repository)
    : IRequestHandler<GetCheckoutSessionByIdQuery, CheckoutSessionResponseDto>
{
    public async Task<CheckoutSessionResponseDto> Handle(
        GetCheckoutSessionByIdQuery query,
        CancellationToken cancellationToken)
    {
        var session = await repository.GetByIdWithItemsAsync(query.CheckoutSessionId, cancellationToken)
                      ?? throw new CheckoutSessionNotFoundException(query.CheckoutSessionId);

        return CheckoutSessionMapper.ToResponseDto(session);
    }
}
