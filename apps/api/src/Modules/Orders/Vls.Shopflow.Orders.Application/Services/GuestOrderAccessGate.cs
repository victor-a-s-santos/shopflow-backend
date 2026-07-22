using Microsoft.Extensions.Options;
using Vls.Shopflow.Orders.Application.Interfaces;
using Vls.Shopflow.Orders.Application.Options;
using Vls.Shopflow.Orders.Application.Repositories;
using Vls.Shopflow.Orders.Domain.Constants;
using Vls.Shopflow.Orders.Domain.Entities;
using Vls.Shopflow.Orders.Domain.Exceptions;

namespace Vls.Shopflow.Orders.Application.Services;

public sealed class GuestOrderAccessGate(
    IGuestOrderAccessTokenRepository guestTokenRepository,
    IGuestOrderAccessTokenHasher tokenHasher,
    IOrderRepository orderRepository,
    IOptions<GuestOrderAccessOptions> guestOrderAccessOptions)
    : IGuestOrderAccessGate
{
    public async Task<(GuestOrderAccessToken Token, Order Order)> ValidateAsync(
        Guid orderId,
        string? rawAccessToken,
        CancellationToken cancellationToken)
    {
        if (!guestOrderAccessOptions.Value.Enabled)
            throw new GuestOrderAccessDeniedException(GuestOrderErrorCodes.OrderNotFoundOrAccessDenied);

        if (string.IsNullOrWhiteSpace(rawAccessToken))
            throw new GuestOrderAccessDeniedException(GuestOrderErrorCodes.InvalidGuestOrderToken);

        string tokenHash;
        try
        {
            tokenHash = tokenHasher.Hash(rawAccessToken);
        }
        catch (GuestOrderAccessMisconfiguredException)
        {
            throw;
        }
        catch
        {
            throw new GuestOrderAccessDeniedException(GuestOrderErrorCodes.InvalidGuestOrderToken);
        }

        var now = DateTimeOffset.UtcNow;
        var accessToken = await guestTokenRepository.FindActiveByOrderIdAndHashAsync(
            orderId,
            tokenHash,
            now,
            cancellationToken);

        if (accessToken is null)
        {
            var anyMatch = await guestTokenRepository.FindByOrderIdAndHashAsync(
                orderId,
                tokenHash,
                cancellationToken);

            if (anyMatch is not null && anyMatch.ExpiresAt <= now)
                throw new GuestOrderAccessTokenExpiredException();

            throw new GuestOrderAccessDeniedException(GuestOrderErrorCodes.InvalidGuestOrderToken);
        }

        var order = await orderRepository.GetByIdWithItemsAsync(orderId, cancellationToken);
        if (order is null)
            throw new GuestOrderAccessDeniedException(GuestOrderErrorCodes.OrderNotFoundOrAccessDenied);

        return (accessToken, order);
    }
}
