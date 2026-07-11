namespace Vls.Shopflow.Orders.Domain.Entities;

public sealed class GuestOrderAccessToken
{
    public const string DefaultPurpose = "GuestOrderStatus";
    public const string HmacSha256Algorithm = "HMAC-SHA256";

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public string TokenHash { get; private set; } = default!;
    public string TokenHashAlgorithm { get; private set; } = default!;
    public string Purpose { get; private set; } = default!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public DateTimeOffset? LastUsedAt { get; private set; }
    public int UsageCount { get; private set; }

    private GuestOrderAccessToken() { }

    public static GuestOrderAccessToken Create(
        Guid orderId,
        string tokenHash,
        DateTimeOffset expiresAt,
        DateTimeOffset? createdAt = null,
        string purpose = DefaultPurpose,
        string algorithm = HmacSha256Algorithm)
    {
        if (orderId == Guid.Empty)
            throw new ArgumentException("Order id is required.", nameof(orderId));

        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new ArgumentException("Token hash is required.", nameof(tokenHash));

        var now = createdAt ?? DateTimeOffset.UtcNow;
        if (expiresAt <= now)
            throw new ArgumentOutOfRangeException(nameof(expiresAt), "Token expiration must be in the future.");

        return new GuestOrderAccessToken
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            TokenHash = tokenHash.Trim(),
            TokenHashAlgorithm = algorithm,
            Purpose = purpose,
            CreatedAt = now,
            ExpiresAt = expiresAt,
            RevokedAt = null,
            LastUsedAt = null,
            UsageCount = 0
        };
    }

    public bool IsActive(DateTimeOffset asOfUtc)
        => RevokedAt is null && ExpiresAt > asOfUtc;

    public void MarkUsed(DateTimeOffset? usedAt = null)
    {
        LastUsedAt = usedAt ?? DateTimeOffset.UtcNow;
        UsageCount++;
    }

    public void Revoke(DateTimeOffset? revokedAt = null)
    {
        if (RevokedAt is not null)
            return;

        RevokedAt = revokedAt ?? DateTimeOffset.UtcNow;
    }
}
