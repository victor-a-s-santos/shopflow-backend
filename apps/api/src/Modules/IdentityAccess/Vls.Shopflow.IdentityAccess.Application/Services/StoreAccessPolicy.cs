using Microsoft.Extensions.Options;
using Vls.Shopflow.IdentityAccess.Application.DataTransferObjects;
using Vls.Shopflow.IdentityAccess.Application.Interfaces;
using Vls.Shopflow.IdentityAccess.Application.Options;
using Vls.Shopflow.IdentityAccess.Domain.Constants;
using Vls.Shopflow.IdentityAccess.Domain.Enums;

namespace Vls.Shopflow.IdentityAccess.Application.Services;

public sealed class StoreAccessPolicy : IStoreAccessPolicy
{
    private readonly bool _configuredAllowGuestCheckout;

    public StoreAccessPolicy(
        IOptions<StoreAccessOptions> storeAccessOptions,
        IOptions<CheckoutAccessOptions> checkoutOptions,
        IOptions<CustomerAccessOptions> customerAccessOptions)
    {
        Mode = ParseMode(storeAccessOptions.Value.Mode);
        _configuredAllowGuestCheckout = checkoutOptions.Value.AllowGuestCheckout;
        RequireApproval = customerAccessOptions.Value.RequireApproval;
        AllowGuestCheckout = _configuredAllowGuestCheckout
                             && Mode == StoreAccessMode.PublicCatalogAndGuestCheckout;
        RequireApprovedCustomerToBrowse = Mode == StoreAccessMode.PrivateCatalogApprovedOnly;
        RequireLoginForCheckout = !AllowGuestCheckout;
        RequireApprovedCustomerForCheckout = Mode is
            StoreAccessMode.PublicCatalogApprovedCheckout or
            StoreAccessMode.PrivateCatalogApprovedOnly;
    }

    public StoreAccessMode Mode { get; }
    public bool AllowGuestCheckout { get; }
    public bool RequireApproval { get; }
    public bool RequireApprovedCustomerToBrowse { get; }
    public bool RequireLoginForCheckout { get; }
    public bool RequireApprovedCustomerForCheckout { get; }

    public StoreAccessDto ToPublicDto()
        => new(
            Mode.ToString(),
            AllowGuestCheckout,
            RequireApprovedCustomerToBrowse,
            RequireLoginForCheckout,
            RequireApprovedCustomerForCheckout);

    public StoreAccessDecision EvaluateBrowse(CustomerUserDto? customer)
    {
        if (!RequireApprovedCustomerToBrowse)
            return StoreAccessDecision.Allow();

        return EvaluateApprovedCustomer(customer);
    }

    public StoreAccessDecision EvaluateCheckout(CustomerUserDto? customer)
    {
        if (AllowGuestCheckout && customer is null)
            return StoreAccessDecision.Allow();

        if (customer is null)
        {
            return _configuredAllowGuestCheckout
                ? Deny(
                    401,
                    StoreAccessErrorCodes.CustomerLoginRequired,
                    "Login is required.")
                : Deny(
                    401,
                    StoreAccessErrorCodes.GuestCheckoutDisabled,
                    "Guest checkout is disabled.");
        }

        var hardBlock = EvaluateRejectedOrSuspended(customer);
        if (hardBlock is not null)
            return hardBlock;

        if (!RequireApprovedCustomerForCheckout)
            return StoreAccessDecision.Allow();

        return EvaluateApprovedCustomer(customer);
    }

    private static StoreAccessDecision EvaluateApprovedCustomer(CustomerUserDto? customer)
    {
        if (customer is null)
        {
            return Deny(
                401,
                StoreAccessErrorCodes.CustomerLoginRequired,
                "Login is required.");
        }

        return EvaluateRejectedOrSuspended(customer)
               ?? (customer.AccessStatus == CustomerAccessStatus.Approved
                   ? StoreAccessDecision.Allow()
                   : Deny(
                       403,
                       StoreAccessErrorCodes.CustomerAccessNotApproved,
                       "Customer access is not approved."));
    }

    private static StoreAccessDecision? EvaluateRejectedOrSuspended(CustomerUserDto customer)
        => customer.AccessStatus switch
        {
            CustomerAccessStatus.Rejected => Deny(
                403,
                StoreAccessErrorCodes.CustomerAccessRejected,
                "Customer access was rejected."),
            CustomerAccessStatus.Suspended => Deny(
                403,
                StoreAccessErrorCodes.CustomerAccessSuspended,
                "Customer access is suspended."),
            _ => null
        };

    private static StoreAccessDecision Deny(int status, string code, string message)
        => StoreAccessDecision.Deny(status, code, message);

    internal static StoreAccessMode ParseMode(string? raw)
    {
        if (Enum.TryParse<StoreAccessMode>(raw, ignoreCase: true, out var mode)
            && Enum.IsDefined(mode))
        {
            return mode;
        }

        return StoreAccessMode.PrivateCatalogApprovedOnly;
    }
}
