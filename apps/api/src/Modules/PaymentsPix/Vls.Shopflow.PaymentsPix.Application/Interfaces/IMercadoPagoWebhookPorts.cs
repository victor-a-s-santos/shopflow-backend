namespace Vls.Shopflow.PaymentsPix.Application.Interfaces;

public interface IMercadoPagoWebhookSignatureValidator
{
    bool IsValid(string? xSignature, string? xRequestId, string dataId, string secret, out string? failureReason);
}

public sealed record MercadoPagoOrderLookup(
    string Id,
    string Status,
    string? StatusDetail,
    string? ExternalReference,
    decimal TotalAmount,
    string? TransactionId,
    decimal? TransactionAmount,
    string? TransactionStatus,
    string? TransactionStatusDetail,
    string? PaymentMethodId,
    string? PaymentMethodType,
    string? QrCode,
    string? QrCodeBase64,
    string? TicketUrl,
    DateTimeOffset? LastUpdatedDate,
    DateTimeOffset? CreatedDate);

public interface IMercadoPagoOrderClient
{
    Task<MercadoPagoOrderLookup?> GetOrderAsync(string orderId, CancellationToken cancellationToken);
}

public interface IOrderPaidWriter
{
    Task<OrderPaidWriteResult> GetAsync(Guid orderId, CancellationToken cancellationToken);

    Task<OrderPaidWriteResult> MarkAsPaidAsync(Guid orderId, DateTimeOffset paidAt, CancellationToken cancellationToken);
}

public sealed record OrderPaidWriteResult(
    bool Found,
    bool AlreadyPaid,
    bool MarkedPaid,
    string? Status,
    Guid? CheckoutSessionId);

public interface ICheckoutReservationIdsReader
{
    Task<IReadOnlyList<Guid>> GetReservationIdsByCheckoutSessionAsync(
        Guid checkoutSessionId,
        CancellationToken cancellationToken);
}

public interface IInventoryReservationConfirmer
{
    Task ConfirmAsync(Guid reservationId, CancellationToken cancellationToken);
}
