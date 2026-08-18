using MediatR;
using Vls.Shopflow.Catalog.Application.Queries;
using Vls.Shopflow.IdentityAccess.Domain.Constants;

namespace Vls.Shopflow.HttpApi.Endpoints;

public static class AdminCatalogEndpoints
{
    public static RouteGroupBuilder MapAdminCatalogEndpoints(this RouteGroupBuilder group)
    {
        var adminCatalog = group.MapGroup("/admin/catalog")
            .WithTags("AdminCatalog")
            .RequireAuthorization(AuthPolicies.Backoffice);

        adminCatalog.MapGet("/products", async (
            ISender sender,
            int? page,
            int? pageSize,
            string? sort,
            string? q,
            string? categorySlug,
            Guid? categoryId,
            string? status,
            string? featured,
            CancellationToken ct) =>
        {
            var result = await sender.Send(
                new GetAdminProductsQuery(
                    page ?? 1,
                    pageSize ?? 20,
                    sort ?? AdminProductListSort.Default,
                    q,
                    categorySlug,
                    categoryId,
                    status ?? AdminProductListFilters.StatusAll,
                    featured ?? AdminProductListFilters.FeaturedAll),
                ct);

            return Results.Ok(result);
        });

        return group;
    }
}
