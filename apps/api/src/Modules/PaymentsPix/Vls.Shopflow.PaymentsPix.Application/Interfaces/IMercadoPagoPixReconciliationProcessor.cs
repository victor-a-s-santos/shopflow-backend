namespace Vls.Shopflow.PaymentsPix.Application.Interfaces;

public interface IMercadoPagoPixReconciliationProcessor
{
    Task<MercadoPagoPixReconciliationBatchResult> ProcessAsync(CancellationToken cancellationToken);
}

public sealed record MercadoPagoPixReconciliationBatchResult(
    int Candidates,
    int Processed,
    int MarkedPaid,
    int StillPending,
    int TerminalUpdated,
    int LookupsSkipped,
    int Failures);
