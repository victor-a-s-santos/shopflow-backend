namespace Vls.Shopflow.Orders.Application.Interfaces;

public interface IGuestOrderAccessTokenHasher
{
    string Hash(string rawToken);

    string GenerateRawToken();
}

public interface IOrderPixPaymentStatusReader
{
    Task<OrderPixPaymentStatusSnapshot?> GetLatestByOrderIdAsync(
        Guid orderId,
        CancellationToken cancellationToken);
}

public sealed record OrderPixPaymentStatusSnapshot(
    string Status,
    string Provider,
    decimal Amount,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? PaidAt,
    DateTimeOffset UpdatedAt);
