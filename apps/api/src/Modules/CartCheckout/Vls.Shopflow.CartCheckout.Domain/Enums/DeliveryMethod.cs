namespace Vls.Shopflow.CartCheckout.Domain.Enums;

/// <summary>
/// Customer preferred delivery method at checkout.
/// Values mirror Orders.Domain.Enums.DeliveryMethod (Carrier / ExcursionBus / Correios).
/// </summary>
public enum DeliveryMethod
{
    Carrier = 0,
    ExcursionBus = 1,
    Correios = 2
}
