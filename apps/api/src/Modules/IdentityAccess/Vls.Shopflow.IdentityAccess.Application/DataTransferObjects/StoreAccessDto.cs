namespace Vls.Shopflow.IdentityAccess.Application.DataTransferObjects;

public sealed record StoreAccessDto(
    string Mode,
    bool AllowGuestCheckout,
    bool RequireApprovedCustomerToBrowse,
    bool RequireLoginForCheckout,
    bool RequireApprovedCustomerForCheckout);

public sealed record StoreAccessDecision(
    bool Allowed,
    int StatusCode,
    string? Code,
    string? Message)
{
    public static StoreAccessDecision Allow()
        => new(true, 200, null, null);

    public static StoreAccessDecision Deny(int statusCode, string code, string message)
        => new(false, statusCode, code, message);
}
