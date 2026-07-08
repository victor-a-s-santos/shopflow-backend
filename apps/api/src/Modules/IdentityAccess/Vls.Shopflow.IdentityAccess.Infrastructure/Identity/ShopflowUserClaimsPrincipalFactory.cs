using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Vls.Shopflow.IdentityAccess.Domain.Constants;
using Vls.Shopflow.IdentityAccess.Infrastructure.Identity;

namespace Vls.Shopflow.IdentityAccess.Infrastructure.Identity;

public sealed class ShopflowUserClaimsPrincipalFactory(
    UserManager<ShopflowUser> userManager,
    RoleManager<ShopflowRole> roleManager,
    IOptions<IdentityOptions> optionsAccessor)
    : UserClaimsPrincipalFactory<ShopflowUser, ShopflowRole>(userManager, roleManager, optionsAccessor)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ShopflowUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim(AuthClaims.IsStaff, user.IsStaff ? "true" : "false"));

        var roles = await UserManager.GetRolesAsync(user);
        var isCustomer = roles.Contains(AuthRoles.Customer) && !user.IsStaff;
        identity.AddClaim(new Claim(AuthClaims.IsCustomer, isCustomer ? "true" : "false"));

        if (!string.IsNullOrWhiteSpace(user.FullName))
            identity.AddClaim(new Claim(AuthClaims.FullName, user.FullName));

        return identity;
    }
}
