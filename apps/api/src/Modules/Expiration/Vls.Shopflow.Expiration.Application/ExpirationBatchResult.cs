namespace Vls.Shopflow.Expiration.Application;

public sealed class ExpirationBatchResult
{
    public int Processed { get; set; }

    public int ExpiredCheckoutSessions { get; set; }

    public int ExpiredOrders { get; set; }

    public int ExpiredPixPayments { get; set; }

    public int CanceledReservations { get; set; }

    public int Failures { get; set; }
}
