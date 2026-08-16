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
        _configuredAllowGuestCheckout = checkoutOptions.Value.GuestCheckoutEnabled;
        RequireApproval = customerAccessOptions.Value.RequireApproval
                          || Mode == StoreAccessMode.PrivateCatalogApprovedOnly
                          || Mode == StoreAccessMode.PublicCatalogApprovedCheckout;
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
            CustomerAccessContract.ToPublicMode(Mode),
            Mode.ToString(),
            AllowGuestCheckout,
            AllowGuestCheckout,
            RequireApprovedCustomerToBrowse,
            RequireLoginForCheckout,
            RequireApprovedCustomerForCheckout);

    public StoreAccessDecision EvaluateBrowse(CustomerUserDto? customer)
    {
        if (!RequireApprovedCustomerToBrowse)
            return StoreAccessDecision.Allow();

        return EvaluateApprovedCustomer(customer, catalog: true);
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
                    StoreAccessMessages.LoginRequiredToBuy)
                : Deny(
                    401,
                    StoreAccessErrorCodes.GuestCheckoutDisabled,
                    StoreAccessMessages.GuestCheckoutDisabled);
        }

        var hardBlock = EvaluateRejectedOrSuspended(customer);
        if (hardBlock is not null)
            return hardBlock;

        if (!RequireApprovedCustomerForCheckout)
            return StoreAccessDecision.Allow();

        return EvaluateApprovedCustomer(customer, catalog: false);
    }

    private static StoreAccessDecision EvaluateApprovedCustomer(CustomerUserDto? customer, bool catalog)
    {
        if (customer is null)
        {
            return catalog
                ? Deny(
                    401,
                    StoreAccessErrorCodes.StoreAccessRequiresLogin,
                    StoreAccessMessages.StoreRequiresApprovedCustomer)
                : Deny(
                    401,
                    StoreAccessErrorCodes.CustomerLoginRequired,
                    StoreAccessMessages.LoginRequiredToBuy);
        }

        var hardBlock = EvaluateRejectedOrSuspended(customer);
        if (hardBlock is not null)
            return hardBlock;

        if (customer.AccessStatus == CustomerAccessStatus.Approved)
            return StoreAccessDecision.Allow();

        if (customer.AccessStatus == CustomerAccessStatus.PendingApproval)
        {
            return catalog
                ? Deny(
                    403,
                    StoreAccessErrorCodes.StoreAccessRequiresApproval,
                    StoreAccessMessages.StoreRequiresApprovedCustomer)
                : Deny(
                    403,
                    StoreAccessErrorCodes.CustomerApprovalPending,
                    StoreAccessMessages.ApprovalPending);
        }

        return Deny(
            403,
            StoreAccessErrorCodes.CustomerAccessNotApproved,
            StoreAccessMessages.LoginRequiredToBuy);
    }

    private static StoreAccessDecision? EvaluateRejectedOrSuspended(CustomerUserDto customer)
        => customer.AccessStatus switch
        {
            CustomerAccessStatus.Rejected => Deny(
                403,
                StoreAccessErrorCodes.CustomerAccessRejected,
                StoreAccessMessages.AccessRejected),
            CustomerAccessStatus.Suspended => Deny(
                403,
                StoreAccessErrorCodes.CustomerAccessSuspended,
                StoreAccessMessages.AccessSuspended),
            _ => null
        };

    private static StoreAccessDecision Deny(int status, string code, string message)
        => StoreAccessDecision.Deny(status, code, message);

    internal static StoreAccessMode ParseMode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return StoreAccessMode.PrivateCatalogApprovedOnly;

        var value = raw.Trim();
        if (value.Equals("Closed", StringComparison.OrdinalIgnoreCase))
            return StoreAccessMode.PrivateCatalogApprovedOnly;
        if (value.Equals("Open", StringComparison.OrdinalIgnoreCase))
            return StoreAccessMode.PublicCatalogAndGuestCheckout;

        if (Enum.TryParse<StoreAccessMode>(value, ignoreCase: true, out var mode)
            && Enum.IsDefined(mode))
        {
            return mode;
        }

        return StoreAccessMode.PrivateCatalogApprovedOnly;
    }

    public static bool TryParseAccessStatus(string? raw, out CustomerAccessStatus status)
    {
        status = default;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var value = raw.Trim();
        if (value.Equals("Pending", StringComparison.OrdinalIgnoreCase)
            || value.Equals("PendingApproval", StringComparison.OrdinalIgnoreCase))
        {
            status = CustomerAccessStatus.PendingApproval;
            return true;
        }

        if (Enum.TryParse<CustomerAccessStatus>(value, ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed))
        {
            status = parsed;
            return true;
        }

        return false;
    }
}
