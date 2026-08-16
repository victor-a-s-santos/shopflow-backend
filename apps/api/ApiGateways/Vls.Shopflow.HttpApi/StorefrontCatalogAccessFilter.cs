using Vls.Shopflow.IdentityAccess.Application.Interfaces;

namespace Vls.Shopflow.HttpApi.Endpoints;

public static class StorefrontCatalogAccessFilter
{
    public static RouteHandlerBuilder RequireApprovedCatalogAccess(this RouteHandlerBuilder builder)
        => builder.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
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
}
