using Vls.Shopflow.IdentityAccess.Domain.Constants;

namespace Vls.Shopflow.IdentityAccess.Domain.Exceptions;

public sealed class StoreAccessDeniedException : Exception
{
    public int StatusCode { get; }
    public string Code { get; }

    public StoreAccessDeniedException(int statusCode, string code, string message)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }

    public static StoreAccessDeniedException LoginRequired()
        => new(401, StoreAccessErrorCodes.CustomerLoginRequired, StoreAccessMessages.LoginRequiredToBuy);

    public static StoreAccessDeniedException GuestCheckoutDisabled()
        => new(401, StoreAccessErrorCodes.GuestCheckoutDisabled, StoreAccessMessages.GuestCheckoutDisabled);

    public static StoreAccessDeniedException ApprovalPending()
        => new(403, StoreAccessErrorCodes.CustomerApprovalPending, StoreAccessMessages.ApprovalPending);

    public static StoreAccessDeniedException NotApproved()
        => new(403, StoreAccessErrorCodes.CustomerAccessNotApproved, StoreAccessMessages.LoginRequiredToBuy);

    public static StoreAccessDeniedException Rejected()
        => new(403, StoreAccessErrorCodes.CustomerAccessRejected, StoreAccessMessages.AccessRejected);

    public static StoreAccessDeniedException Suspended()
        => new(403, StoreAccessErrorCodes.CustomerAccessSuspended, StoreAccessMessages.AccessSuspended);

    public static StoreAccessDeniedException CatalogRequiresLogin()
        => new(401, StoreAccessErrorCodes.StoreAccessRequiresLogin, StoreAccessMessages.StoreRequiresApprovedCustomer);

    public static StoreAccessDeniedException CatalogRequiresApproval()
        => new(403, StoreAccessErrorCodes.StoreAccessRequiresApproval, StoreAccessMessages.StoreRequiresApprovedCustomer);
}

public sealed class CustomerApprovalException : Exception
{
    public int StatusCode { get; }
    public string Code { get; }

    public CustomerApprovalException(int statusCode, string code, string message)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }

    public static CustomerApprovalException InvalidTransition(string? message = null)
        => new(
            409,
            StoreAccessErrorCodes.CustomerApprovalInvalidStatus,
            string.IsNullOrWhiteSpace(message) ? StoreAccessMessages.InvalidApprovalStatus : message);

    public static CustomerApprovalException ReasonTooLong()
        => new(400, StoreAccessErrorCodes.CustomerApprovalReasonTooLong, StoreAccessMessages.ReasonTooLong);

    public static CustomerApprovalException NotFound()
        => new(404, StoreAccessErrorCodes.CustomerNotFound, StoreAccessMessages.CustomerNotFound);
}
