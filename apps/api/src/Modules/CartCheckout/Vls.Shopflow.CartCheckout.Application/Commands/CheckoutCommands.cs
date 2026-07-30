using Vls.Shopflow.BuildingBlocks.Application.Interfaces;
using Vls.Shopflow.CartCheckout.Application.DataTransferObjects;

namespace Vls.Shopflow.CartCheckout.Application.Commands;

public sealed record CreateCheckoutSessionCommand(
    CustomerInput Customer,
    AddressInput Address,
    IReadOnlyList<CheckoutItemInput> Items,
    string? PreferredDeliveryMethod = null,
    DateOnly? PreferredDeliveryDate = null,
    string? CustomerOrderNote = null
) : ICommand<CheckoutSessionResponseDto>;

public sealed record CustomerInput(string FullName, string Email, string Phone);

public sealed record AddressInput(
    string ZipCode,
    string Street,
    string Number,
    string? Complement,
    string Neighborhood,
    string City,
    string State);

public sealed record CheckoutItemInput(Guid SkuId, int Quantity);

public sealed record CancelCheckoutSessionCommand(Guid CheckoutSessionId) : ICommand;
