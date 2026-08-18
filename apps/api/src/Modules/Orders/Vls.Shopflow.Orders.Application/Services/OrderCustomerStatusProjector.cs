using Vls.Shopflow.Orders.Domain.Enums;

namespace Vls.Shopflow.Orders.Application.Services;

/// <summary>
/// Stable public status codes for consumer surfaces. Portuguese labels belong to the frontend.
/// </summary>
public static class OrderCustomerStatusCodes
{
    public const string AwaitingPayment = "AwaitingPayment";
    public const string Confirmed = "Confirmed";
    public const string Canceled = "Canceled";
    public const string Expired = "Expired";
}

public static class OrderCustomerStatusProjector
{
    public const string PaymentMethodPix = "Pix";

    public static string Project(OrderStatus orderStatus, string? paymentStatus)
    {
        if (orderStatus == OrderStatus.Canceled)
            return OrderCustomerStatusCodes.Canceled;

        if (orderStatus == OrderStatus.Expired)
            return OrderCustomerStatusCodes.Expired;

        if (orderStatus == OrderStatus.Paid
            || string.Equals(paymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
            return OrderCustomerStatusCodes.Confirmed;

        if (string.Equals(paymentStatus, "Expired", StringComparison.OrdinalIgnoreCase))
            return OrderCustomerStatusCodes.Expired;

        return OrderCustomerStatusCodes.AwaitingPayment;
    }

    /// <summary>
    /// Accepts public customerStatus codes or domain OrderStatus names for list filters.
    /// </summary>
    public static bool TryParseListFilter(string? raw, out OrderStatus status)
    {
        status = default;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var value = raw.Trim();

        if (string.Equals(value, OrderCustomerStatusCodes.AwaitingPayment, StringComparison.OrdinalIgnoreCase))
        {
            status = OrderStatus.PendingPayment;
            return true;
        }

        if (string.Equals(value, OrderCustomerStatusCodes.Confirmed, StringComparison.OrdinalIgnoreCase))
        {
            status = OrderStatus.Paid;
            return true;
        }

        if (string.Equals(value, OrderCustomerStatusCodes.Canceled, StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, nameof(OrderStatus.Canceled), StringComparison.OrdinalIgnoreCase))
        {
            status = OrderStatus.Canceled;
            return true;
        }

        if (string.Equals(value, OrderCustomerStatusCodes.Expired, StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, nameof(OrderStatus.Expired), StringComparison.OrdinalIgnoreCase))
        {
            status = OrderStatus.Expired;
            return true;
        }

        return Enum.TryParse(value, ignoreCase: true, out status);
    }

    public static DateTimeOffset? ActivePaymentExpiresAt(string paymentStatus, DateTimeOffset? expiresAt)
        => string.Equals(paymentStatus, "Pending", StringComparison.OrdinalIgnoreCase)
            ? expiresAt
            : null;
}
