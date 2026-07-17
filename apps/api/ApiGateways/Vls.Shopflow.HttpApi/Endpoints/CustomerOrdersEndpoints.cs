using MediatR;
using Vls.Shopflow.IdentityAccess.Application.Interfaces;
using Vls.Shopflow.IdentityAccess.Domain.Constants;
using Vls.Shopflow.IdentityAccess.Infrastructure;
using Vls.Shopflow.Orders.Application.Commands;
using Vls.Shopflow.Orders.Application.Queries;

namespace Vls.Shopflow.HttpApi.Endpoints;

public static class CustomerOrdersEndpoints
{
    public static RouteGroupBuilder MapCustomerOrdersEndpoints(this RouteGroupBuilder group)
    {
        var customerOrders = group.MapGroup("/customer/orders")
            .WithTags("CustomerOrders");

        // Guest claim — anonymous create-account (token proves possession)
        customerOrders.MapPost("/guest/{orderId:guid}/create-account", async (
            ISender sender,
            Guid orderId,
            CreateAccountFromGuestOrderRequest req,
            CancellationToken ct) =>
        {
            var result = await sender.Send(
                new CreateAccountFromGuestOrderCommand(
                    orderId,
                    req.GuestAccessToken,
                    req.Password,
                    req.ConfirmPassword),
                ct);

            return Results.Ok(new
            {
                orderId = result.OrderId,
                customerCreated = result.CustomerCreated,
                orderLinked = result.OrderLinked,
                redirectTo = result.RedirectTo
            });
        })
        .AllowAnonymous()
        .RequireRateLimiting(DependencyInjection.GuestOrderClaimRateLimitPolicy)
        .DisableAntiforgery();

        // Guest claim — authenticated customer with matching email
        customerOrders.MapPost("/guest/{orderId:guid}/claim", async (
            ISender sender,
            ICurrentCustomerAccessor currentCustomer,
            Guid orderId,
            ClaimGuestOrderRequest req,
            CancellationToken ct) =>
        {
            var customer = await currentCustomer.GetCurrentCustomerAsync(ct);
            if (customer is null)
                return Results.Unauthorized();

            var result = await sender.Send(
                new ClaimGuestOrderCommand(
                    orderId,
                    req.GuestAccessToken,
                    customer.CustomerId,
                    customer.Email),
                ct);

            return Results.Ok(new
            {
                orderId = result.OrderId,
                orderLinked = result.OrderLinked,
                redirectTo = result.RedirectTo
            });
        })
        .RequireAuthorization(AuthPolicies.Customer)
        .RequireRateLimiting(DependencyInjection.GuestOrderClaimRateLimitPolicy);

        var authenticated = customerOrders
            .MapGroup("")
            .RequireAuthorization(AuthPolicies.Customer);

        authenticated.MapGet("", async (
            ISender sender,
            ICurrentCustomerAccessor currentCustomer,
            int? page,
            int? pageSize,
            string? status,
            string? paymentStatus,
            DateTimeOffset? createdFrom,
            DateTimeOffset? createdTo,
            string? sort,
            CancellationToken ct) =>
        {
            var customer = await currentCustomer.GetCurrentCustomerAsync(ct);
            if (customer is null)
                return Results.Unauthorized();

            var result = await sender.Send(
                new GetCustomerOrdersQuery(
                    customer.CustomerId,
                    page ?? 1,
                    pageSize ?? 10,
                    status,
                    paymentStatus,
                    createdFrom,
                    createdTo,
                    sort),
                ct);

            return Results.Ok(result);
        });

        authenticated.MapGet("/{orderId:guid}", async (
            ISender sender,
            ICurrentCustomerAccessor currentCustomer,
            Guid orderId,
            CancellationToken ct) =>
        {
            var customer = await currentCustomer.GetCurrentCustomerAsync(ct);
            if (customer is null)
                return Results.Unauthorized();

            var result = await sender.Send(
                new GetCustomerOrderByIdQuery(customer.CustomerId, orderId),
                ct);

            return Results.Ok(result);
        });

        return group;
    }
}

public sealed record CreateAccountFromGuestOrderRequest(
    string GuestAccessToken,
    string Password,
    string ConfirmPassword);

public sealed record ClaimGuestOrderRequest(string GuestAccessToken);
