namespace Vls.Shopflow.Orders.Application.DataTransferObjects;

public sealed record CreateAccountFromGuestOrderResult(
    Guid OrderId,
    bool CustomerCreated,
    bool OrderLinked,
    string RedirectTo);

public sealed record ClaimGuestOrderResult(
    Guid OrderId,
    bool OrderLinked,
    string RedirectTo);
