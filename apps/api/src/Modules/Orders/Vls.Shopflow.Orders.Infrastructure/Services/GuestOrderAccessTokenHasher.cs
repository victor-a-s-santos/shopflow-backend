using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Vls.Shopflow.Orders.Application.Interfaces;
using Vls.Shopflow.Orders.Application.Options;
using Vls.Shopflow.Orders.Domain.Exceptions;

namespace Vls.Shopflow.Orders.Infrastructure.Services;

public sealed class GuestOrderAccessTokenHasher(IOptions<GuestOrderAccessOptions> options)
    : IGuestOrderAccessTokenHasher
{
    public string GenerateRawToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    public string Hash(string rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
            throw new ArgumentException("Raw token is required.", nameof(rawToken));

        var secret = options.Value.TokenHashSecret;
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new GuestOrderAccessMisconfiguredException(
                "GuestOrderAccess:TokenHashSecret is required when guest order access is enabled.");
        }

        var key = Encoding.UTF8.GetBytes(secret);
        var data = Encoding.UTF8.GetBytes(rawToken.Trim());
        var hash = HMACSHA256.HashData(key, data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    internal static string Base64UrlEncode(ReadOnlySpan<byte> bytes)
    {
        var base64 = Convert.ToBase64String(bytes);
        return base64.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
