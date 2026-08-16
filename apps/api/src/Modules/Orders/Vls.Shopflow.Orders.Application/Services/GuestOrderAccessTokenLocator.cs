namespace Vls.Shopflow.Orders.Application.Services;

/// <summary>
/// Resolves the guest order access token from the HTTP request.
/// Header is preferred; query <c>t</c> matches transactional email deep-links;
/// query <c>token</c> is the legacy alias.
/// </summary>
public static class GuestOrderAccessTokenLocator
{
    public static string? Resolve(string? header, string? queryT, string? queryToken)
    {
        if (!string.IsNullOrWhiteSpace(header))
            return header.Trim();
        if (!string.IsNullOrWhiteSpace(queryT))
            return queryT.Trim();
        if (!string.IsNullOrWhiteSpace(queryToken))
            return queryToken.Trim();
        return null;
    }
}
