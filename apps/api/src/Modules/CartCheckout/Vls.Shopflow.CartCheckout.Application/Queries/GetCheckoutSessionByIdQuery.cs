using Vls.Shopflow.BuildingBlocks.Application.Interfaces;
using Vls.Shopflow.CartCheckout.Application.DataTransferObjects;

namespace Vls.Shopflow.CartCheckout.Application.Queries;

public sealed record GetCheckoutSessionByIdQuery(Guid CheckoutSessionId)
    : IQuery<CheckoutSessionResponseDto>;
