using Vls.Shopflow.PaymentsPix.Domain.Entities;

namespace Vls.Shopflow.PaymentsPix.Application.Mappers;

internal static class PixPaymentMapper
{
    public const string PreparationMessage =
        "Pagamento Pix criado em modo preparação. Gateway real ainda não integrado.";

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
            payment.ExpiresAt,
            payment.CreatedAt,
            PreparationMessage);

    public static void EnsureOrderCanReceivePixPayment(string status, Guid orderId)
    {
        if (!string.Equals(status, "PendingPayment", StringComparison.Ordinal))
        {
            throw new Domain.Exceptions.OrderNotEligibleForPixPaymentException(orderId, status);
        }
    }
}
