using Vls.Shopflow.PaymentsPix.Domain.Entities;

namespace Vls.Shopflow.PaymentsPix.Application.Mappers;

internal static class PixPaymentMapper
{
    public const string PreparationMessage =
        "Pagamento Pix criado em modo preparação. Gateway real ainda não integrado.";

    public const string MercadoPagoMessage =
        "Pix gerado. Aguardando pagamento.";

    public static string ResolveMessage(Domain.Enums.PixPaymentProviderType provider)
        => provider == Domain.Enums.PixPaymentProviderType.MercadoPago
            ? MercadoPagoMessage
            : PreparationMessage;

    public static DataTransferObjects.PixPaymentDto ToDto(PixPayment payment)
        => new(
            payment.Id,
            payment.OrderId,
            payment.Status.ToString(),
            payment.Provider.ToString(),
            payment.Amount,
            payment.QrCode,
            payment.QrCodeImageUrl,
            payment.CopyPasteCode,
            payment.TicketUrl,
            payment.ExpiresAt,
            payment.CreatedAt,
            ResolveMessage(payment.Provider));

    public static void EnsureOrderCanReceivePixPayment(string status, Guid orderId)
    {
        if (!string.Equals(status, "PendingPayment", StringComparison.Ordinal))
        {
            throw new Domain.Exceptions.OrderNotEligibleForPixPaymentException(orderId, status);
        }
    }
}
