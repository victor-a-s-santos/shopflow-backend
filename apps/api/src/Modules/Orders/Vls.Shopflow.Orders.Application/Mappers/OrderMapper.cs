using Vls.Shopflow.Orders.Domain.Entities;

namespace Vls.Shopflow.Orders.Application.Mappers;

internal static class OrderMapper
{
    public static DataTransferObjects.OrderDto ToDto(
        Order order,
        string? guestAccessToken = null,
        DateTimeOffset? guestAccessTokenExpiresAt = null)
        => new(
            order.Id,
            order.Id.ToString(),
            order.CheckoutSessionId,
            order.Status.ToString(),
            new DataTransferObjects.OrderCustomerDto(
                order.CustomerFullName,
                order.CustomerEmail,
                order.CustomerPhone),
            new DataTransferObjects.OrderAddressDto(
                order.ShippingZipCode,
                order.ShippingStreet,
                order.ShippingNumber,
                order.ShippingComplement,
                order.ShippingNeighborhood,
                order.ShippingCity,
                order.ShippingState),
            order.Items.Select(i => new DataTransferObjects.OrderItemDto(
                i.SkuId,
                i.ProductName,
                i.SkuCode,
                i.Quantity,
                i.UnitPrice,
                i.Subtotal)).ToList(),
            order.Subtotal,
            order.ShippingAmount,
            order.Total,
            order.CreatedAt,
            guestAccessToken,
            guestAccessTokenExpiresAt);

    public static DataTransferObjects.GuestOrderStatusDto ToGuestStatusDto(
        Order order,
        Interfaces.OrderPixPaymentStatusSnapshot? payment,
        GuestOrderAccessToken accessToken)
        => new(
            order.Id,
            order.Id.ToString(),
            order.Status.ToString(),
            payment is null
                ? null
                : new DataTransferObjects.GuestOrderPaymentStatusDto(
                    payment.Status,
                    payment.Provider,
                    payment.Amount,
                    payment.ExpiresAt,
                    payment.PaidAt,
                    payment.UpdatedAt),
            order.Items.Select(i => new DataTransferObjects.GuestOrderItemStatusDto(
                i.ProductName,
                i.SkuId,
                i.Quantity,
                i.UnitPrice,
                i.Subtotal,
                Attributes: null,
                ImageUrl: null)).ToList(),
            new DataTransferObjects.GuestOrderTotalsDto(
                order.Subtotal,
                Discount: 0m,
                order.ShippingAmount,
                order.Total),
            new DataTransferObjects.GuestOrderMaskedCustomerDto(
                MaskName(order.CustomerFullName),
                MaskEmail(order.CustomerEmail)),
            new DataTransferObjects.GuestOrderAccessMetaDto(
                accessToken.ExpiresAt,
                accessToken.LastUsedAt));

    public static void EnsureCheckoutSessionCanCreateOrder(string status, Guid checkoutSessionId)
    {
        if (!string.Equals(status, "Pending", StringComparison.Ordinal))
        {
            throw new Domain.Exceptions.InvalidCheckoutSessionForOrderException(
                checkoutSessionId,
                $"Checkout session {checkoutSessionId} cannot create an order because its status is {status}.");
        }
    }

    internal static string MaskName(string fullName)
    {
        var trimmed = fullName.Trim();
        if (trimmed.Length == 0)
            return "***";

        if (trimmed.Length == 1)
            return trimmed + "***";

        return trimmed[..2] + "***";
    }

    internal static string MaskEmail(string email)
    {
        var trimmed = email.Trim();
        var at = trimmed.IndexOf('@');
        if (at <= 0 || at == trimmed.Length - 1)
            return "***";

        var local = trimmed[..at];
        var domain = trimmed[(at + 1)..];
        var localMasked = local.Length == 0 ? "***" : $"{char.ToLowerInvariant(local[0])}***";
        return $"{localMasked}@{domain}";
    }
}
