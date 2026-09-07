using MediatR;
using Vls.Shopflow.CartCheckout.Application.Commands;
using Vls.Shopflow.CartCheckout.Application.Queries;
using Vls.Shopflow.IdentityAccess.Application.DataTransferObjects;
using Vls.Shopflow.IdentityAccess.Application.Interfaces;

namespace Vls.Shopflow.HttpApi.Endpoints;

public static class CheckoutEndpoints
{
    public static RouteGroupBuilder MapCheckoutEndpoints(this RouteGroupBuilder group)
    {
        var checkout = group.MapGroup("/checkout").WithTags("Checkout");

        checkout.MapPost("/sessions", async (
            HttpContext ctx,
            ISender sender,
            IStoreAccessPolicy storeAccess,
            ICurrentCustomerAccessor currentCustomer,
            ICustomerAddressService addresses,
            CreateCheckoutSessionRequest request,
            CancellationToken ct) =>
        {
            var denied = await StoreAccessHttp.EnsureCheckoutAllowedAsync(
                ctx, storeAccess, currentCustomer, ct);
            if (denied is not null)
                return denied;

            var customer = await currentCustomer.GetCurrentCustomerAsync(ct);
            var resolved = await ResolveAddressAsync(customer, addresses, request, ct);
            if (resolved.Error is not null)
                return resolved.Error;

            var result = await sender.Send(
                new CreateCheckoutSessionCommand(
                    new CustomerInput(request.Customer.FullName, request.Customer.Email, request.Customer.Phone),
                    resolved.Address!,
                    request.Items.Select(i => new CheckoutItemInput(i.SkuId, i.Quantity)).ToList(),
                    request.PreferredDeliveryMethod,
                    request.PreferredDeliveryDate,
                    request.CustomerOrderNote),
                ct);

            if (request.SaveAddress
                && request.CustomerAddressId is null
                && request.Address is not null
                && customer is not null)
            {
                await addresses.CreateAsync(
                    customer.CustomerId,
                    new CustomerAddressWriteRequest(
                        Label: null,
                        RecipientName: request.Customer.FullName,
                        PostalCode: request.Address.ZipCode,
                        Street: request.Address.Street,
                        Number: request.Address.Number,
                        Complement: request.Address.Complement,
                        Neighborhood: request.Address.Neighborhood,
                        City: request.Address.City,
                        State: request.Address.State,
                        SetAsDefault: request.SetAsDefault),
                    ct);
            }

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

    internal static async Task<(AddressInput? Address, IResult? Error)> ResolveAddressAsync(
        CustomerUserDto? customer,
        ICustomerAddressService addresses,
        CreateCheckoutSessionRequest request,
        CancellationToken ct)
    {
        if (request.CustomerAddressId is Guid savedId)
        {
            if (customer is null)
            {
                return (null, Results.Json(
                    new { code = "CUSTOMER_LOGIN_REQUIRED", message = "É necessário estar autenticado para usar um endereço salvo." },
                    statusCode: StatusCodes.Status401Unauthorized));
            }

            var saved = await addresses.GetOwnedAsync(customer.CustomerId, savedId, ct);
            if (saved is null)
            {
                return (null, Results.Json(
                    new { code = "ADDRESS_NOT_FOUND", message = "Endereço não encontrado." },
                    statusCode: StatusCodes.Status400BadRequest));
            }

            return (new AddressInput(
                saved.PostalCode,
                saved.Street,
                saved.Number,
                saved.Complement,
                saved.Neighborhood,
                saved.City,
                saved.State), null);
        }

        if (request.Address is null)
        {
            return (null, Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["address"] = ["Informe o endereço de entrega ou um endereço salvo."]
                }));
        }

        return (new AddressInput(
            request.Address.ZipCode,
            request.Address.Street,
            request.Address.Number,
            request.Address.Complement,
            request.Address.Neighborhood,
            request.Address.City,
            request.Address.State), null);
    }
}

public sealed record CreateCheckoutSessionRequest(
    CustomerRequest Customer,
    AddressRequest? Address,
    IReadOnlyList<CheckoutItemRequest> Items,
    string? PreferredDeliveryMethod = null,
    DateOnly? PreferredDeliveryDate = null,
    string? CustomerOrderNote = null,
    Guid? CustomerAddressId = null,
    bool SaveAddress = false,
    bool SetAsDefault = false);

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
