using Vls.Shopflow.PaymentsPix.Domain.Entities;

namespace Vls.Shopflow.PaymentsPix.Application.Interfaces;

public interface IMercadoPagoPixPaidTransitionService
{
    /// <summary>
    /// Marks Shopflow order + PixPayment Paid and confirms reservations (idempotent).
    /// Persists PixPayment changes via unit of work. Does not touch webhook events.
    /// </summary>
    Task<MercadoPagoPixPaidTransitionResult> ApplyPaidAsync(
        PixPayment pixPayment,
        MercadoPagoOrderLookup mpOrder,
        CancellationToken cancellationToken);
}

public sealed record MercadoPagoPixPaidTransitionResult(
    bool Success,
    string Outcome,
    string Message);
