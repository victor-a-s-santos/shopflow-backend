using Vls.Shopflow.CartCheckout.Application.Commands;
using Vls.Shopflow.CartCheckout.Domain.Entities;
using Vls.Shopflow.CartCheckout.Domain.Enums;
using Vls.Shopflow.CartCheckout.Domain.Exceptions;

namespace Vls.Shopflow.CartCheckout.Application.Mappers;

internal static class CheckoutSessionMapper
{
  public static DataTransferObjects.CheckoutSessionResponseDto ToResponseDto(CheckoutSession session)
        => new(
            session.Id,
            session.Status.ToString(),
            session.Items.Select(i => new DataTransferObjects.CheckoutSessionItemDto(
                i.SkuId,
                i.ProductName,
                i.SkuCode,
                i.Quantity,
                i.UnitPrice,
                i.Subtotal)).ToList(),
            session.Subtotal,
            session.ShippingAmount,
            session.Total,
            new DataTransferObjects.CheckoutPaymentDto(
                "Pix",
                "NotImplemented",
                "Pagamento Pix será integrado no módulo PaymentsPix."),
            session.PreferredDeliveryMethod?.ToString(),
            session.PreferredDeliveryDate,
            session.CustomerOrderNote);

    public static void EnsureCanCancel(CheckoutSession session)
    {
        if (session.Status == CheckoutSessionStatus.Canceled)
            return;

        if (session.Status != CheckoutSessionStatus.Pending)
            throw new InvalidCheckoutSessionStatusException(
                session.Id,
                $"Checkout session {session.Id} cannot be canceled because its status is {session.Status}.");
    }
}
