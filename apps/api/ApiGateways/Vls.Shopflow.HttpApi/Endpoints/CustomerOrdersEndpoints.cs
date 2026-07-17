using MediatR;
using Vls.Shopflow.IdentityAccess.Application.Interfaces;
using Vls.Shopflow.IdentityAccess.Domain.Constants;
using Vls.Shopflow.Orders.Application.Queries;

namespace Vls.Shopflow.HttpApi.Endpoints;

public static class CustomerOrdersEndpoints
{
    public static RouteGroupBuilder MapCustomerOrdersEndpoints(this RouteGroupBuilder group)
    {
        var customerOrders = group.MapGroup("/customer/orders")
            .WithTags("CustomerOrders")
            .RequireAuthorization(AuthPolicies.Customer);

        customerOrders.MapGet("", async (
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

        customerOrders.MapGet("/{orderId:guid}", async (
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
