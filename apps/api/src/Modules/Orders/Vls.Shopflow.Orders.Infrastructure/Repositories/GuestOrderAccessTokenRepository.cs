using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.Orders.Application.Repositories;
using Vls.Shopflow.Orders.Domain.Entities;

namespace Vls.Shopflow.Orders.Infrastructure.Repositories;

public sealed class GuestOrderAccessTokenRepository(OrdersDbContext db) : IGuestOrderAccessTokenRepository
{
    public async Task AddAsync(GuestOrderAccessToken token, CancellationToken cancellationToken)
        => await db.GuestOrderAccessTokens.AddAsync(token, cancellationToken);

    public Task<GuestOrderAccessToken?> FindActiveByOrderIdAndHashAsync(
        Guid orderId,
        string tokenHash,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken)
        => db.GuestOrderAccessTokens.FirstOrDefaultAsync(
            t => t.OrderId == orderId
                 && t.TokenHash == tokenHash
                 && t.RevokedAt == null
                 && t.ExpiresAt > asOfUtc,
            cancellationToken);
}
