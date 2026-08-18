namespace Vls.Shopflow.Orders.Application.DataTransferObjects;

public sealed record CreateAccountFromGuestOrderResult(
    string Code,
    Guid OrderId,
    string OrderNumber,
    bool CustomerCreated,
    bool OrderLinked,
    string RedirectTo);

public sealed record ClaimGuestOrderResult(
    string Code,
    Guid OrderId,
    string OrderNumber,
    bool OrderLinked,
    string RedirectTo);
