namespace Vls.Shopflow.Orders.Domain.Constants;

/// <summary>
/// Official guest-order / claim API codes for frontend flow control.
/// </summary>
public static class GuestOrderErrorCodes
{
    public const string AccountAlreadyExists = "ACCOUNT_ALREADY_EXISTS";
    public const string PasswordRequirementsNotMet = "PASSWORD_REQUIREMENTS_NOT_MET";
    public const string InvalidGuestOrderToken = "INVALID_GUEST_ORDER_TOKEN";
    public const string GuestOrderTokenExpired = "GUEST_ORDER_TOKEN_EXPIRED";
    public const string OrderAlreadyLinked = "ORDER_ALREADY_LINKED";
    public const string OrderLinkedToAnotherCustomer = "ORDER_LINKED_TO_ANOTHER_CUSTOMER";
    public const string CustomerEmailDoesNotMatchOrder = "CUSTOMER_EMAIL_DOES_NOT_MATCH_ORDER";
    public const string OrderNotFoundOrAccessDenied = "ORDER_NOT_FOUND_OR_ACCESS_DENIED";
    public const string AccountCreatedAndOrderLinked = "ACCOUNT_CREATED_AND_ORDER_LINKED";
    public const string OrderLinked = "ORDER_LINKED";
}
