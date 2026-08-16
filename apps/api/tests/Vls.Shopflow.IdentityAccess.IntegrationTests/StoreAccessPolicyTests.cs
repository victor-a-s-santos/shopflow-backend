using FluentAssertions;
using Microsoft.Extensions.Options;
using Vls.Shopflow.IdentityAccess.Application.DataTransferObjects;
using Vls.Shopflow.IdentityAccess.Application.Options;
using Vls.Shopflow.IdentityAccess.Application.Services;
using Vls.Shopflow.IdentityAccess.Domain.Constants;
using Vls.Shopflow.IdentityAccess.Domain.Enums;

namespace Vls.Shopflow.IdentityAccess.IntegrationTests;

public sealed class StoreAccessPolicyTests
{
    [Fact]
    public void PrivateMode_AnonymousBrowseAndCheckout_AreDenied()
    {
        var policy = CreatePolicy("PrivateCatalogApprovedOnly", allowGuest: false, requireApproval: true);

        policy.RequireApprovedCustomerToBrowse.Should().BeTrue();
        policy.AllowGuestCheckout.Should().BeFalse();
        policy.RequireApprovedCustomerForCheckout.Should().BeTrue();

        var browse = policy.EvaluateBrowse(null);
        browse.Allowed.Should().BeFalse();
        browse.StatusCode.Should().Be(401);
        browse.Code.Should().Be(StoreAccessErrorCodes.CustomerLoginRequired);

        var checkout = policy.EvaluateCheckout(null);
        checkout.Allowed.Should().BeFalse();
        checkout.Code.Should().Be(StoreAccessErrorCodes.GuestCheckoutDisabled);
    }

    [Fact]
    public void PrivateMode_PendingCustomer_CannotBrowseOrCheckout()
    {
        var policy = CreatePolicy("PrivateCatalogApprovedOnly", allowGuest: false, requireApproval: true);
        var pending = Customer(CustomerAccessStatus.PendingApproval);

        policy.EvaluateBrowse(pending).Code.Should().Be(StoreAccessErrorCodes.CustomerAccessNotApproved);
        policy.EvaluateCheckout(pending).Code.Should().Be(StoreAccessErrorCodes.CustomerAccessNotApproved);
    }

    [Fact]
    public void PrivateMode_RejectedAndSuspended_ReturnDedicatedCodes()
    {
        var policy = CreatePolicy("PrivateCatalogApprovedOnly", allowGuest: false, requireApproval: true);

        policy.EvaluateCheckout(Customer(CustomerAccessStatus.Rejected)).Code
            .Should().Be(StoreAccessErrorCodes.CustomerAccessRejected);
        policy.EvaluateCheckout(Customer(CustomerAccessStatus.Suspended)).Code
            .Should().Be(StoreAccessErrorCodes.CustomerAccessSuspended);
    }

    [Fact]
    public void PrivateMode_ApprovedCustomer_CanBrowseAndCheckout()
    {
        var policy = CreatePolicy("PrivateCatalogApprovedOnly", allowGuest: false, requireApproval: true);
        var approved = Customer(CustomerAccessStatus.Approved);

        policy.EvaluateBrowse(approved).Allowed.Should().BeTrue();
        policy.EvaluateCheckout(approved).Allowed.Should().BeTrue();
    }

    [Fact]
    public void PublicGuestMode_AllowsAnonymousWhenFlagTrue()
    {
        var policy = CreatePolicy("PublicCatalogAndGuestCheckout", allowGuest: true, requireApproval: false);

        policy.EvaluateBrowse(null).Allowed.Should().BeTrue();
        policy.EvaluateCheckout(null).Allowed.Should().BeTrue();
        policy.AllowGuestCheckout.Should().BeTrue();
    }

    [Fact]
    public void PublicGuestMode_FlagFalse_BlocksAnonymousCheckout()
    {
        var policy = CreatePolicy("PublicCatalogAndGuestCheckout", allowGuest: false, requireApproval: false);

        policy.EvaluateBrowse(null).Allowed.Should().BeTrue();
        policy.EvaluateCheckout(null).Code.Should().Be(StoreAccessErrorCodes.GuestCheckoutDisabled);
    }

    [Fact]
    public void LoginCheckout_PendingCanCheckout_RejectedCannot()
    {
        var policy = CreatePolicy("PublicCatalogLoginCheckout", allowGuest: false, requireApproval: false);

        policy.EvaluateBrowse(null).Allowed.Should().BeTrue();
        policy.EvaluateCheckout(null).Code.Should().Be(StoreAccessErrorCodes.GuestCheckoutDisabled);
        policy.EvaluateCheckout(Customer(CustomerAccessStatus.PendingApproval)).Allowed.Should().BeTrue();
        policy.EvaluateCheckout(Customer(CustomerAccessStatus.Rejected)).Code
            .Should().Be(StoreAccessErrorCodes.CustomerAccessRejected);
    }

    [Fact]
    public void UnknownMode_FailsClosedToPrivate()
    {
        var policy = CreatePolicy("Closed", allowGuest: true, requireApproval: false);
        policy.Mode.Should().Be(StoreAccessMode.PrivateCatalogApprovedOnly);
        policy.RequireApprovedCustomerToBrowse.Should().BeTrue();
        policy.AllowGuestCheckout.Should().BeFalse();
    }

    private static StoreAccessPolicy CreatePolicy(string mode, bool allowGuest, bool requireApproval)
        => new(
            Options.Create(new StoreAccessOptions { Mode = mode }),
            Options.Create(new CheckoutAccessOptions { AllowGuestCheckout = allowGuest }),
            Options.Create(new CustomerAccessOptions { RequireApproval = requireApproval }));

    private static CustomerUserDto Customer(CustomerAccessStatus status)
        => new(
            Guid.NewGuid(),
            "c@test.local",
            "Customer",
            "11999999999",
            false,
            ["Customer"],
            status,
            DateTimeOffset.UtcNow,
            status == CustomerAccessStatus.Approved ? DateTimeOffset.UtcNow : null);
}
