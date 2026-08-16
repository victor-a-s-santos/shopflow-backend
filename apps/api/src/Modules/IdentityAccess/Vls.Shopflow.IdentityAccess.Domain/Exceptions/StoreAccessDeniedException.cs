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
        => new(401, StoreAccessErrorCodes.CustomerLoginRequired, "Login is required.");

    public static StoreAccessDeniedException GuestCheckoutDisabled()
        => new(401, StoreAccessErrorCodes.GuestCheckoutDisabled, "Guest checkout is disabled.");

    public static StoreAccessDeniedException NotApproved()
        => new(403, StoreAccessErrorCodes.CustomerAccessNotApproved, "Customer access is not approved.");

    public static StoreAccessDeniedException Rejected()
        => new(403, StoreAccessErrorCodes.CustomerAccessRejected, "Customer access was rejected.");

    public static StoreAccessDeniedException Suspended()
        => new(403, StoreAccessErrorCodes.CustomerAccessSuspended, "Customer access is suspended.");
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

    public static CustomerApprovalException InvalidTransition(string message)
        => new(409, StoreAccessErrorCodes.CustomerAccessInvalidTransition, message);
}
