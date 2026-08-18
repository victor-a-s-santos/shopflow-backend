using MediatR;
using Vls.Shopflow.IdentityAccess.Application.Queries;

namespace Vls.Shopflow.HttpApi.Endpoints;

public static class StoreAccessEndpoints
{
    public static RouteGroupBuilder MapStoreAccessEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/store/access", async (ISender sender, CancellationToken ct) =>
        {
            var dto = await sender.Send(new GetStoreAccessQuery(), ct);
            return Results.Ok(new
            {
                mode = dto.Mode,
                storeAccessMode = dto.StoreAccessMode,
                allowGuest = dto.AllowGuest,
                allowGuestCheckout = dto.AllowGuestCheckout,
                requireApprovedCustomerToBrowse = dto.RequireApprovedCustomerToBrowse,
                requireLoginForCheckout = dto.RequireLoginForCheckout,
                requireApprovedCustomerForCheckout = dto.RequireApprovedCustomerForCheckout
            });
        })
        .WithTags("StoreAccess")
        .AllowAnonymous();

        return group;
    }
}
