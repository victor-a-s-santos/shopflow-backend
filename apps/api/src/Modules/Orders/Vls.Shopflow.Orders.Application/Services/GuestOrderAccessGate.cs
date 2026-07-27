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
        EnsureEnabled();
        var tokenHash = HashOrDeny(rawAccessToken);
        var accessToken = await ResolveActiveTokenAsync(orderId, tokenHash, cancellationToken);

        var order = await orderRepository.GetByIdWithItemsAsync(orderId, cancellationToken);
        if (order is null)
            throw new GuestOrderAccessDeniedException(GuestOrderErrorCodes.OrderNotFoundOrAccessDenied);

        return (accessToken, order);
    }

    public async Task<(GuestOrderAccessToken Token, Order Order)> ValidateByOrderNumberAsync(
        long orderNumber,
        string? rawAccessToken,
        CancellationToken cancellationToken)
    {
        EnsureEnabled();

        if (orderNumber <= 0)
            throw new GuestOrderAccessDeniedException(GuestOrderErrorCodes.InvalidGuestOrderToken);

        var tokenHash = HashOrDeny(rawAccessToken);

        // Resolve order before token so wrong number + any token matches wrong-token responses
        // (no distinct "order missing" signal for public enumeration).
        var order = await orderRepository.GetByOrderNumberWithItemsAsync(orderNumber, cancellationToken);
        if (order is null)
            throw new GuestOrderAccessDeniedException(GuestOrderErrorCodes.InvalidGuestOrderToken);

        var accessToken = await ResolveActiveTokenAsync(order.Id, tokenHash, cancellationToken);
        return (accessToken, order);
    }

    private void EnsureEnabled()
    {
        if (!guestOrderAccessOptions.Value.Enabled)
            throw new GuestOrderAccessDeniedException(GuestOrderErrorCodes.OrderNotFoundOrAccessDenied);
    }

    private string HashOrDeny(string? rawAccessToken)
    {
        if (string.IsNullOrWhiteSpace(rawAccessToken))
            throw new GuestOrderAccessDeniedException(GuestOrderErrorCodes.InvalidGuestOrderToken);

        try
        {
            return tokenHasher.Hash(rawAccessToken);
        }
        catch (GuestOrderAccessMisconfiguredException)
        {
            throw;
        }
        catch
        {
            throw new GuestOrderAccessDeniedException(GuestOrderErrorCodes.InvalidGuestOrderToken);
        }
    }

    private async Task<GuestOrderAccessToken> ResolveActiveTokenAsync(
        Guid orderId,
        string tokenHash,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var accessToken = await guestTokenRepository.FindActiveByOrderIdAndHashAsync(
            orderId,
            tokenHash,
            now,
            cancellationToken);

        if (accessToken is not null)
            return accessToken;

        var anyMatch = await guestTokenRepository.FindByOrderIdAndHashAsync(
            orderId,
            tokenHash,
            cancellationToken);

        if (anyMatch is not null && anyMatch.ExpiresAt <= now)
            throw new GuestOrderAccessTokenExpiredException();

        throw new GuestOrderAccessDeniedException(GuestOrderErrorCodes.InvalidGuestOrderToken);
    }
}
