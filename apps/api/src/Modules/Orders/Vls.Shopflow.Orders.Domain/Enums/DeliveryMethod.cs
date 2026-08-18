namespace Vls.Shopflow.Orders.Domain.Enums;

/// <summary>
/// Preferred or final delivery method. UI labels are frontend/docs responsibility.
/// </summary>
public enum DeliveryMethod
{
    /// <summary>Transportadora</summary>
    Carrier = 0,

    /// <summary>Ônibus de excursão</summary>
    ExcursionBus = 1,

    /// <summary>Correios</summary>
    Correios = 2
}
