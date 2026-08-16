namespace Vls.Shopflow.IdentityAccess.Domain.Constants;

public static class StoreAccessErrorCodes
{
    public const string CustomerLoginRequired = "CUSTOMER_LOGIN_REQUIRED";
    public const string CustomerAccessNotApproved = "CUSTOMER_ACCESS_NOT_APPROVED";
    public const string CustomerAccessRejected = "CUSTOMER_ACCESS_REJECTED";
    public const string CustomerAccessSuspended = "CUSTOMER_ACCESS_SUSPENDED";
    public const string GuestCheckoutDisabled = "GUEST_CHECKOUT_DISABLED";
    public const string CustomerAccessInvalidTransition = "CUSTOMER_ACCESS_INVALID_TRANSITION";
}
