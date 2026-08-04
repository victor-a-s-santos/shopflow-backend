namespace Vls.Shopflow.PaymentsPix.Application.Interfaces;

public sealed record MercadoPagoWebhookSignatureDiagnostics(
    bool HasXSignature,
    bool HasXRequestId,
    bool HasQueryDataId,
    bool DataIdQueryWasLowercased,
    bool TsPresent,
    bool V1Present,
    bool SecretConfigured,
    long? TimestampAgeSeconds,
    bool? TimestampWithinTolerance,
    string? ReceivedV1Prefix,
    string? ComputedOfficialPrefix,
    string ManifestPartsIncluded,
    string? QueryDataIdMasked,
    string? RequestIdMasked,
    string FailureReasonCode,
    bool? SdkSignatureValid = null,
    bool? ManualSignatureValid = null,
    string SignatureValidatorFinal = "Rejected",
    string? SdkExceptionType = null,
    string? ManualFailureReason = null,
    int? SecretLength = null,
    bool? SecretTrimmedChanged = null,
    string? WebhookSecretFingerprint = null);

public sealed record MercadoPagoWebhookSignatureValidationResult(
    bool IsValid,
    string? FailureReason,
    string FailureReasonCode,
    MercadoPagoWebhookSignatureDiagnostics Diagnostics);

public interface IMercadoPagoWebhookSignatureValidator
{
    MercadoPagoWebhookSignatureValidationResult Validate(
        string? xSignature,
        string? xRequestId,
        string? queryDataId,
        string? secret);

    /// <summary>Legacy helper used by older tests; prefer <see cref="Validate"/>.</summary>
    bool IsValid(string? xSignature, string? xRequestId, string dataId, string secret, out string? failureReason);
}

/// <summary>Thin adapter over MercadoPago.Webhook.WebhookSignatureValidator for unit tests.</summary>
public interface IMercadoPagoOfficialWebhookSignatureClient
{
    void Validate(
        string xSignature,
        string? xRequestId,
        string? queryDataId,
        string secret,
        TimeSpan? tolerance);
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

public enum MercadoPagoOrderLookupStatus
{
    Found,
    NotFound,
    BadRequest,
    Unauthorized,
    TransientFailure
}

public sealed record MercadoPagoOrderLookupResult(
    MercadoPagoOrderLookupStatus Status,
    MercadoPagoOrderLookup? Order,
    int? HttpStatusCode,
    string? ErrorMessage);

public interface IMercadoPagoOrderClient
{
    Task<MercadoPagoOrderLookupResult> GetOrderAsync(string orderId, CancellationToken cancellationToken);
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
    Guid? CheckoutSessionId,
    long? OrderNumber = null,
    string? CustomerEmail = null,
    string? CustomerFullName = null,
    decimal? Total = null,
    Guid? CustomerUserId = null,
    string? PreferredDeliveryMethod = null,
    DateOnly? PreferredDeliveryDate = null);

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
