using Vls.Shopflow.PaymentsPix.Application.Interfaces;

namespace Vls.Shopflow.PaymentsPix.Application.Services;

public static class MercadoPagoOrderStatusRules
{
    private static readonly HashSet<string> PendingStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "created", "processing", "action_required"
    };

    private static readonly HashSet<string> PendingStatusDetails = new(StringComparer.OrdinalIgnoreCase)
    {
        "waiting_payment", "waiting_transfer"
    };

    public static bool IsPixPaymentMethod(MercadoPagoOrderLookup order)
        => string.Equals(order.PaymentMethodId, "pix", StringComparison.OrdinalIgnoreCase)
           && (string.IsNullOrWhiteSpace(order.PaymentMethodType)
               || string.Equals(order.PaymentMethodType, "bank_transfer", StringComparison.OrdinalIgnoreCase));

    public static bool IsPaid(MercadoPagoOrderLookup order)
    {
        var statusOk = string.Equals(order.Status, "processed", StringComparison.OrdinalIgnoreCase);
        var detailOk = string.Equals(order.StatusDetail, "accredited", StringComparison.OrdinalIgnoreCase);
        if (!statusOk || !detailOk)
            return false;

        if (!string.IsNullOrWhiteSpace(order.TransactionStatus)
            && !string.Equals(order.TransactionStatus, "processed", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrWhiteSpace(order.TransactionStatusDetail)
            && !string.Equals(order.TransactionStatusDetail, "accredited", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    public static bool IsPending(MercadoPagoOrderLookup order)
    {
        if (!PendingStatuses.Contains(order.Status ?? string.Empty))
            return false;

        if (string.IsNullOrWhiteSpace(order.StatusDetail))
            return true;

        return PendingStatusDetails.Contains(order.StatusDetail);
    }

    public static bool AmountsMatch(decimal localAmount, decimal providerAmount)
        => Math.Abs(localAmount - providerAmount) < 0.01m;

    public static bool ExternalReferenceMatches(Guid orderId, string? externalReference)
    {
        if (string.IsNullOrWhiteSpace(externalReference))
            return false;

        return Guid.TryParse(externalReference, out var parsed) && parsed == orderId;
    }
}
