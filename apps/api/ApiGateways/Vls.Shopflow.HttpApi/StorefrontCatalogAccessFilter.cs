using System.Security.Claims;
using Vls.Shopflow.IdentityAccess.Application.Interfaces;
using Vls.Shopflow.IdentityAccess.Domain.Constants;

namespace Vls.Shopflow.HttpApi.Endpoints;

public static class StorefrontCatalogAccessFilter
{
    public static RouteHandlerBuilder RequireApprovedCatalogAccess(this RouteHandlerBuilder builder)
        => builder.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            if (IsBackoffice(http.User))
                return await next(context);

            var policy = http.RequestServices.GetRequiredService<IStoreAccessPolicy>();
            if (!policy.RequireApprovedCustomerToBrowse)
                return await next(context);

            var currentCustomer = http.RequestServices.GetRequiredService<ICurrentCustomerAccessor>();
            var customer = await currentCustomer.GetCurrentCustomerAsync(http.RequestAborted);
            var decision = policy.EvaluateBrowse(customer);
            if (!decision.Allowed)
                return StoreAccessHttp.Denied(http, decision);

            return await next(context);
        });

    private static bool IsBackoffice(ClaimsPrincipal user)
        => user.Identity?.IsAuthenticated == true
           && user.IsInRole(AuthRoles.Owner)
           && string.Equals(
               user.FindFirst(AuthClaims.IsStaff)?.Value,
               "true",
               StringComparison.OrdinalIgnoreCase);
}
