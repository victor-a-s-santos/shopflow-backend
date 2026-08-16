using Vls.Shopflow.IdentityAccess.Application.DataTransferObjects;
using Vls.Shopflow.IdentityAccess.Domain.Enums;

namespace Vls.Shopflow.IdentityAccess.Application.Interfaces;

/// <summary>
/// Backend source of truth for catalog browse, checkout and guest checkout
/// given StoreAccess:Mode and Checkout:AllowGuest.
/// </summary>
public interface IStoreAccessPolicy
{
    StoreAccessMode Mode { get; }
    bool AllowGuestCheckout { get; }
    bool RequireApproval { get; }
    bool RequireApprovedCustomerToBrowse { get; }
    bool RequireLoginForCheckout { get; }
    bool RequireApprovedCustomerForCheckout { get; }

    StoreAccessDto ToPublicDto();
    StoreAccessDecision EvaluateBrowse(CustomerUserDto? customer);
    StoreAccessDecision EvaluateCheckout(CustomerUserDto? customer);
}
