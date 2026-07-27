using MediatR;
using Vls.Shopflow.IdentityAccess.Application.Interfaces;
using Vls.Shopflow.IdentityAccess.Domain.Constants;
using Vls.Shopflow.IdentityAccess.Infrastructure;
using Vls.Shopflow.Orders.Application.Commands;
using Vls.Shopflow.Orders.Application.DataTransferObjects;

namespace Vls.Shopflow.HttpApi.Endpoints;

public static class OrdersEndpoints
{
    public const string OrderAccessTokenHeaderName = "X-ORDER-ACCESS-TOKEN";

    public static RouteGroupBuilder MapOrdersEndpoints(this RouteGroupBuilder group)
    {
        var orders = group.MapGroup("/orders").WithTags("Orders");

        orders.MapPost("/from-checkout-session", async (
            ISender sender,
            ICurrentCustomerAccessor currentCustomer,
            CreateOrderFromCheckoutSessionRequest request,
            CancellationToken ct) =>
        {
            // Optional CustomerCookie only — never use Backoffice cookie as customer.
            var customer = await currentCustomer.GetCurrentCustomerAsync(ct);

            var result = await sender.Send(
                new CreateOrderFromCheckoutSessionCommand(
                    request.CheckoutSessionId,
                    customer?.CustomerId),
                ct);

            return Results.Created($"/api/orders/{result.OrderId}", result);
        });

        orders.MapGet("/guest/{orderId:guid}/status", async (
            ISender sender,
            HttpRequest request,
            Guid orderId,
            CancellationToken ct) =>
        {
            var accessToken = request.Headers[OrderAccessTokenHeaderName].FirstOrDefault();
            var result = await sender.Send(new GetGuestOrderStatusQuery(orderId, accessToken), ct);
            return Results.Ok(result);
        })
        .RequireRateLimiting(DependencyInjection.GuestOrderStatusRateLimitPolicy)
        .AllowAnonymous();

        // Preferred guest tracking: orderNumber + token (header preferred; query allowed for email/deep links).
        orders.MapGet("/public/{orderNumber}", async (
            ISender sender,
            HttpRequest request,
            string orderNumber,
            CancellationToken ct) =>
        {
            var accessToken = request.Headers[OrderAccessTokenHeaderName].FirstOrDefault()
                              ?? request.Query["token"].FirstOrDefault();
            var result = await sender.Send(new GetPublicOrderStatusQuery(orderNumber, accessToken), ct);
            return Results.Ok(result);
        })
        .RequireRateLimiting(DependencyInjection.GuestOrderStatusRateLimitPolicy)
        .AllowAnonymous();

        // Full order data (PII) — backoffice only.
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
