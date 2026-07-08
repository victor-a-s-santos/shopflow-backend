using MediatR;
using Vls.Shopflow.CartCheckout.Application.Commands;
using Vls.Shopflow.CartCheckout.Application.Queries;

namespace Vls.Shopflow.HttpApi.Endpoints;

public static class CheckoutEndpoints
{
    public static RouteGroupBuilder MapCheckoutEndpoints(this RouteGroupBuilder group)
    {
        var checkout = group.MapGroup("/checkout").WithTags("Checkout");

        checkout.MapPost("/sessions", async (
            ISender sender,
            CreateCheckoutSessionRequest request,
            CancellationToken ct) =>
        {
            var result = await sender.Send(
                new CreateCheckoutSessionCommand(
                    new CustomerInput(request.Customer.FullName, request.Customer.Email, request.Customer.Phone),
                    new AddressInput(
                        request.Address.ZipCode,
                        request.Address.Street,
                        request.Address.Number,
                        request.Address.Complement,
                        request.Address.Neighborhood,
                        request.Address.City,
                        request.Address.State),
                    request.Items.Select(i => new CheckoutItemInput(i.SkuId, i.Quantity)).ToList()),
                ct);

            return Results.Created($"/api/checkout/sessions/{result.CheckoutSessionId}", result);
        });

        checkout.MapGet("/sessions/{id:guid}", async (
            ISender sender,
            Guid id,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetCheckoutSessionByIdQuery(id), ct);
            return Results.Ok(result);
        });

        checkout.MapPost("/sessions/{id:guid}/cancel", async (
            ISender sender,
            Guid id,
            CancellationToken ct) =>
        {
            await sender.Send(new CancelCheckoutSessionCommand(id), ct);
            return Results.NoContent();
        });

        return group;
    }
}

public sealed record CreateCheckoutSessionRequest(
    CustomerRequest Customer,
    AddressRequest Address,
    IReadOnlyList<CheckoutItemRequest> Items);

public sealed record CustomerRequest(string FullName, string Email, string Phone);

public sealed record AddressRequest(
    string ZipCode,
    string Street,
    string Number,
    string? Complement,
    string Neighborhood,
    string City,
    string State);

public sealed record CheckoutItemRequest(Guid SkuId, int Quantity);
