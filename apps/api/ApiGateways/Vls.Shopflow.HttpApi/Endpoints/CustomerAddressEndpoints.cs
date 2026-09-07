using Vls.Shopflow.IdentityAccess.Application.DataTransferObjects;
using Vls.Shopflow.IdentityAccess.Application.Interfaces;
using Vls.Shopflow.IdentityAccess.Domain.Constants;

namespace Vls.Shopflow.HttpApi.Endpoints;

public static class CustomerAddressEndpoints
{
    public static RouteGroupBuilder MapCustomerAddressEndpoints(this RouteGroupBuilder group)
    {
        var addresses = group.MapGroup("/customer/addresses").WithTags("CustomerAddresses");

        addresses.MapGet("/", async (
            ICurrentCustomerAccessor currentCustomer,
            ICustomerAddressService addressesService,
            CancellationToken ct) =>
        {
            var customer = await currentCustomer.GetCurrentCustomerAsync(ct);
            if (customer is null)
                return Results.Unauthorized();

            var items = await addressesService.ListAsync(customer.CustomerId, ct);
            return Results.Ok(items);
        })
        .RequireAuthorization(AuthPolicies.Customer);

        addresses.MapGet("/default", async (
            ICurrentCustomerAccessor currentCustomer,
            ICustomerAddressService addressesService,
            CancellationToken ct) =>
        {
            var customer = await currentCustomer.GetCurrentCustomerAsync(ct);
            if (customer is null)
                return Results.Unauthorized();

            var item = await addressesService.GetDefaultAsync(customer.CustomerId, ct);
            return item is null ? Results.NoContent() : Results.Ok(item);
        })
        .RequireAuthorization(AuthPolicies.Customer);

        addresses.MapPost("/", async (
            ICurrentCustomerAccessor currentCustomer,
            ICustomerAddressService addressesService,
            CustomerAddressWriteRequest request,
            CancellationToken ct) =>
        {
            var customer = await currentCustomer.GetCurrentCustomerAsync(ct);
            if (customer is null)
                return Results.Unauthorized();

            var result = await addressesService.CreateAsync(customer.CustomerId, request, ct);
            return ToHttp(result, created: true);
        })
        .RequireAuthorization(AuthPolicies.Customer);

        addresses.MapPut("/{id:guid}", async (
            Guid id,
            ICurrentCustomerAccessor currentCustomer,
            ICustomerAddressService addressesService,
            CustomerAddressWriteRequest request,
            CancellationToken ct) =>
        {
            var customer = await currentCustomer.GetCurrentCustomerAsync(ct);
            if (customer is null)
                return Results.Unauthorized();

            var result = await addressesService.UpdateAsync(customer.CustomerId, id, request, ct);
            return ToHttp(result);
        })
        .RequireAuthorization(AuthPolicies.Customer);

        addresses.MapDelete("/{id:guid}", async (
            Guid id,
            ICurrentCustomerAccessor currentCustomer,
            ICustomerAddressService addressesService,
            CancellationToken ct) =>
        {
            var customer = await currentCustomer.GetCurrentCustomerAsync(ct);
            if (customer is null)
                return Results.Unauthorized();

            var result = await addressesService.DeleteAsync(customer.CustomerId, id, ct);
            if (!result.Succeeded && result.ErrorCode == "ADDRESS_NOT_FOUND")
                return Results.NotFound();
            return Results.NoContent();
        })
        .RequireAuthorization(AuthPolicies.Customer);

        addresses.MapPost("/{id:guid}/default", async (
            Guid id,
            ICurrentCustomerAccessor currentCustomer,
            ICustomerAddressService addressesService,
            CancellationToken ct) =>
        {
            var customer = await currentCustomer.GetCurrentCustomerAsync(ct);
            if (customer is null)
                return Results.Unauthorized();

            var result = await addressesService.SetDefaultAsync(customer.CustomerId, id, ct);
            return ToHttp(result);
        })
        .RequireAuthorization(AuthPolicies.Customer);

        return group;
    }

    private static IResult ToHttp(CustomerAddressResult result, bool created = false)
    {
        if (result.Succeeded && result.Address is not null)
        {
            return created
                ? Results.Created($"/api/customer/addresses/{result.Address.Id}", result.Address)
                : Results.Ok(result.Address);
        }

        if (result.ErrorCode == "ADDRESS_NOT_FOUND")
            return Results.NotFound();

        if (result.Errors is { Count: > 0 })
        {
            return Results.ValidationProblem(
                result.Errors,
                title: result.ErrorMessage,
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = result.ErrorCode
                });
        }

        return Results.BadRequest(new { code = result.ErrorCode, message = result.ErrorMessage });
    }
}
