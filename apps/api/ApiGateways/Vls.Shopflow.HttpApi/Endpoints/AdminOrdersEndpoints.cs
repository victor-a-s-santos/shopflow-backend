using MediatR;
using Vls.Shopflow.IdentityAccess.Domain.Constants;
using Vls.Shopflow.Orders.Application.Queries;

namespace Vls.Shopflow.HttpApi.Endpoints;

public static class AdminOrdersEndpoints
{
    public static RouteGroupBuilder MapAdminOrdersEndpoints(this RouteGroupBuilder group)
    {
        var adminOrders = group.MapGroup("/admin/orders")
            .WithTags("AdminOrders")
            .RequireAuthorization(AuthPolicies.Backoffice);

        adminOrders.MapGet("", async (
            ISender sender,
            int? page,
            int? pageSize,
            string? status,
            string? paymentStatus,
            string? q,
            DateTimeOffset? createdFrom,
            DateTimeOffset? createdTo,
            bool? paidOnly,
            string? sort,
            CancellationToken ct) =>
        {
            var result = await sender.Send(
                new GetAdminOrdersQuery(
                    page ?? 1,
                    pageSize ?? 20,
                    status,
                    paymentStatus,
                    q,
                    createdFrom,
                    createdTo,
                    paidOnly,
                    sort),
                ct);

            return Results.Ok(result);
        });

        adminOrders.MapGet("/{orderId:guid}", async (
            ISender sender,
            Guid orderId,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetAdminOrderByIdQuery(orderId), ct);
            return Results.Ok(result);
        });

        return group;
    }
}
