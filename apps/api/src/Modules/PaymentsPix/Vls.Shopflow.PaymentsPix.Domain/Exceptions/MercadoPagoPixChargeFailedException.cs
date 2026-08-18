namespace Vls.Shopflow.PaymentsPix.Domain.Exceptions;

public sealed class MercadoPagoPixChargeFailedException : Exception
{
    public Guid OrderId { get; }
    public int? StatusCode { get; }
    public string? ProviderMessage { get; }

    public MercadoPagoPixChargeFailedException(
        Guid orderId,
        int? statusCode,
        string? providerMessage,
        string message)
        : base(message)
    {
        OrderId = orderId;
        StatusCode = statusCode;
        ProviderMessage = providerMessage;
    }
}
