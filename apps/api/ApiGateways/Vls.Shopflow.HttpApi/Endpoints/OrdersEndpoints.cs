using MediatR;
using Vls.Shopflow.IdentityAccess.Domain.Constants;
using Vls.Shopflow.Orders.Application.Commands;
using Vls.Shopflow.Orders.Application.DataTransferObjects;

namespace Vls.Shopflow.HttpApi.Endpoints;

public static class OrdersEndpoints
{
    public static RouteGroupBuilder MapOrdersEndpoints(this RouteGroupBuilder group)
    {
        var orders = group.MapGroup("/orders").WithTags("Orders");

        orders.MapPost("/from-checkout-session", async (
            ISender sender,
            CreateOrderFromCheckoutSessionRequest request,
            CancellationToken ct) =>
        {
            var result = await sender.Send(
                new CreateOrderFromCheckoutSessionCommand(request.CheckoutSessionId),
                ct);

            return Results.Created($"/api/orders/{result.OrderId}", result);
        });

        // Full order data (PII) — backoffice only until guest access token exists (Phase 4).
        orders.MapGet("/{orderId:guid}", async (
            ISender sender,
            Guid orderId,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetOrderByIdQuery(orderId), ct);
            return Results.Ok(result);
        })
        .RequireAuthorization(AuthPolicies.Backoffice);

        orders.MapGet("/by-checkout-session/{checkoutSessionId:guid}", async (
            ISender sender,
            Guid checkoutSessionId,
            CancellationToken ct) =>
        {
            var result = await sender.Send(
                new GetOrderByCheckoutSessionIdQuery(checkoutSessionId),
                ct);
            return Results.Ok(result);
        })
        .RequireAuthorization(AuthPolicies.Backoffice);

        return group;
    }
}
