using Vls.Shopflow.Orders.Domain.Entities;

namespace Vls.Shopflow.Orders.Application.Interfaces;

/// <summary>
/// Validates GuestOrderAccessToken and loads the order without leaking existence on failure.
/// </summary>
public interface IGuestOrderAccessGate
{
    /// <summary>
    /// Returns the active token entity and order, or throws <see cref="Domain.Exceptions.GuestOrderAccessDeniedException"/>.
    /// Does not mark the token as used and does not save.
    /// </summary>
    Task<(GuestOrderAccessToken Token, Order Order)> ValidateAsync(
        Guid orderId,
        string? rawAccessToken,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resolves the order by public <paramref name="orderNumber"/> then validates the token.
    /// Invalid number / missing order / wrong token all throw the same access-denied exception.
    /// </summary>
    Task<(GuestOrderAccessToken Token, Order Order)> ValidateByOrderNumberAsync(
        long orderNumber,
        string? rawAccessToken,
        CancellationToken cancellationToken);
}
