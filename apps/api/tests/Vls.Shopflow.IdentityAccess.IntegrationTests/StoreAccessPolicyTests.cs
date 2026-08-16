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
        browse.Code.Should().Be(StoreAccessErrorCodes.StoreAccessRequiresLogin);

        var checkout = policy.EvaluateCheckout(null);
        checkout.Allowed.Should().BeFalse();
        checkout.Code.Should().Be(StoreAccessErrorCodes.GuestCheckoutDisabled);
    }

    [Fact]
    public void PrivateMode_PendingCustomer_CannotBrowseOrCheckout()
    {
        var policy = CreatePolicy("PrivateCatalogApprovedOnly", allowGuest: false, requireApproval: true);
        var pending = Customer(CustomerAccessStatus.PendingApproval);

        policy.EvaluateBrowse(pending).Code.Should().Be(StoreAccessErrorCodes.StoreAccessRequiresApproval);
        policy.EvaluateCheckout(pending).Code.Should().Be(StoreAccessErrorCodes.CustomerApprovalPending);
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
    public void ClosedAlias_MapsToPrivateCatalogApprovedOnly()
    {
        var policy = CreatePolicy("Closed", allowGuest: false, requireApproval: false);
        policy.Mode.Should().Be(StoreAccessMode.PrivateCatalogApprovedOnly);
        policy.RequireApproval.Should().BeTrue();
        policy.RequireApprovedCustomerToBrowse.Should().BeTrue();
        policy.AllowGuestCheckout.Should().BeFalse();

        var dto = policy.ToPublicDto();
        dto.Mode.Should().Be("Closed");
        dto.StoreAccessMode.Should().Be(nameof(StoreAccessMode.PrivateCatalogApprovedOnly));
        dto.AllowGuest.Should().BeFalse();
    }

    [Fact]
    public void OpenAlias_MapsToPublicCatalogAndGuestCheckout()
    {
        var policy = CreatePolicy("Open", allowGuest: true, requireApproval: false);
        policy.Mode.Should().Be(StoreAccessMode.PublicCatalogAndGuestCheckout);
        policy.RequireApproval.Should().BeFalse();
        policy.EvaluateBrowse(null).Allowed.Should().BeTrue();
        policy.EvaluateCheckout(null).Allowed.Should().BeTrue();

        var dto = policy.ToPublicDto();
        dto.Mode.Should().Be("Open");
        dto.StoreAccessMode.Should().Be(nameof(StoreAccessMode.PublicCatalogAndGuestCheckout));
        dto.AllowGuest.Should().BeTrue();
    }

    [Fact]
    public void AllowGuestAlias_EnablesGuestCheckout()
    {
        var policy = new StoreAccessPolicy(
            Options.Create(new StoreAccessOptions { Mode = "PublicCatalogAndGuestCheckout" }),
            Options.Create(new CheckoutAccessOptions { AllowGuest = true }),
            Options.Create(new CustomerAccessOptions { RequireApproval = false }));

        policy.AllowGuestCheckout.Should().BeTrue();
        policy.EvaluateCheckout(null).Allowed.Should().BeTrue();
    }

    [Fact]
    public void UnknownMode_FailsClosedToPrivate()
    {
        var policy = CreatePolicy("NotARealMode", allowGuest: true, requireApproval: false);
        policy.Mode.Should().Be(StoreAccessMode.PrivateCatalogApprovedOnly);
        policy.RequireApprovedCustomerToBrowse.Should().BeTrue();
        policy.AllowGuestCheckout.Should().BeFalse();
        policy.ToPublicDto().Mode.Should().Be("Closed");
    }

    [Fact]
    public void PublicApprovalStatus_PendingApproval_IsPending()
    {
        CustomerAccessContract.ToPublicApprovalStatus(CustomerAccessStatus.PendingApproval)
            .Should().Be("Pending");
        CustomerAccessContract.ToPublicApprovalStatus(CustomerAccessStatus.Approved)
            .Should().Be("Approved");
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
