using System.Text.Json.Serialization;

namespace Vls.Shopflow.PaymentsPix.Infrastructure.MercadoPago;

internal sealed class MercadoPagoCreateOrderRequest
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "online";

    [JsonPropertyName("external_reference")]
    public string ExternalReference { get; init; } = string.Empty;

    [JsonPropertyName("total_amount")]
    public string TotalAmount { get; init; } = string.Empty;

    [JsonPropertyName("processing_mode")]
    public string ProcessingMode { get; init; } = "automatic";

    /// <summary>
    /// Optional. When set, MP prioritizes this URL over the Webhooks panel URL.
    /// Omit (null) to use panel-configured Webhooks only.
    /// </summary>
    [JsonPropertyName("notification_url")]
    public string? NotificationUrl { get; init; }

    [JsonPropertyName("payer")]
    public MercadoPagoOrderPayerRequest Payer { get; init; } = new();

    [JsonPropertyName("transactions")]
    public MercadoPagoOrderTransactionsRequest Transactions { get; init; } = new();
}

internal sealed class MercadoPagoOrderPayerRequest
{
    [JsonPropertyName("email")]
    public string Email { get; init; } = string.Empty;

    [JsonPropertyName("first_name")]
    public string FirstName { get; init; } = string.Empty;

    [JsonPropertyName("last_name")]
    public string? LastName { get; init; }
}

internal sealed class MercadoPagoOrderTransactionsRequest
{
    [JsonPropertyName("payments")]
    public MercadoPagoOrderPaymentRequest[] Payments { get; init; } = [];
}

internal sealed class MercadoPagoOrderPaymentRequest
{
    [JsonPropertyName("amount")]
    public string Amount { get; init; } = string.Empty;

    [JsonPropertyName("payment_method")]
    public MercadoPagoOrderPaymentMethodRequest PaymentMethod { get; init; } = new();

    [JsonPropertyName("expiration_time")]
    public string? ExpirationTime { get; init; }
}

internal sealed class MercadoPagoOrderPaymentMethodRequest
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "pix";

    [JsonPropertyName("type")]
    public string Type { get; init; } = "bank_transfer";
}

internal sealed class MercadoPagoOrderResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("status_detail")]
    public string? StatusDetail { get; init; }

    [JsonPropertyName("external_reference")]
    public string? ExternalReference { get; init; }

    [JsonPropertyName("total_amount")]
    public string? TotalAmount { get; init; }

    [JsonPropertyName("created_date")]
    public DateTimeOffset? CreatedDate { get; init; }

    [JsonPropertyName("last_updated_date")]
    public DateTimeOffset? LastUpdatedDate { get; init; }

    [JsonPropertyName("transactions")]
    public MercadoPagoOrderTransactionsResponse? Transactions { get; init; }
}

internal sealed class MercadoPagoOrderTransactionsResponse
{
    [JsonPropertyName("payments")]
    public MercadoPagoOrderPaymentResponse[]? Payments { get; init; }
}

internal sealed class MercadoPagoOrderPaymentResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("amount")]
    public string? Amount { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("status_detail")]
    public string? StatusDetail { get; init; }

    [JsonPropertyName("date_of_expiration")]
    public DateTimeOffset? DateOfExpiration { get; init; }

    [JsonPropertyName("payment_method")]
    public MercadoPagoOrderPaymentMethodResponse? PaymentMethod { get; init; }
}

internal sealed class MercadoPagoOrderPaymentMethodResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("qr_code")]
    public string? QrCode { get; init; }

    [JsonPropertyName("qr_code_base64")]
    public string? QrCodeBase64 { get; init; }

    [JsonPropertyName("ticket_url")]
    public string? TicketUrl { get; init; }
}

/// <summary>
/// Error envelope for Mercado Pago Orders API failures (incl. HTTP 402 transaction failures).
/// 402 often returns <c>errors[]</c> + nested <c>data</c> (order/transactions) rather than a flat message-only body.
/// </summary>
internal sealed class MercadoPagoErrorResponse
{
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("cause")]
    public MercadoPagoErrorCause[]? Cause { get; init; }

    [JsonPropertyName("errors")]
    public MercadoPagoApiError[]? Errors { get; init; }

    /// <summary>Nested order snapshot on some failure responses (e.g. HTTP 402).</summary>
    [JsonPropertyName("data")]
    public MercadoPagoErrorOrderData? Data { get; init; }

    // Flat order fields when MP returns the order body with a non-2xx status.
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("status_detail")]
    public string? StatusDetail { get; init; }

    [JsonPropertyName("transactions")]
    public MercadoPagoOrderTransactionsResponse? Transactions { get; init; }
}

internal sealed class MercadoPagoErrorOrderData
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("status_detail")]
    public string? StatusDetail { get; init; }

    [JsonPropertyName("transactions")]
    public MercadoPagoOrderTransactionsResponse? Transactions { get; init; }
}

internal sealed class MercadoPagoErrorCause
{
    [JsonPropertyName("code")]
    public string? Code { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

internal sealed class MercadoPagoApiError
{
    [JsonPropertyName("code")]
    public string? Code { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("details")]
    public string[]? Details { get; init; }
}

/// <summary>
/// Safe, structured snapshot of a Mercado Pago create-order failure for logging/diagnostics.
/// Does not include payer PII, tokens, QR codes, or raw response bodies.
/// </summary>
internal sealed record MercadoPagoCreateOrderFailureDetails(
    int HttpStatusCode,
    string? ProviderMessage,
    string? Error,
    string? ProviderOrderId,
    string? OrderStatus,
    string? OrderStatusDetail,
    string? TransactionId,
    string? TransactionStatus,
    string? TransactionStatusDetail,
    string? ErrorCode,
    string? CauseCode,
    string? CauseDescription,
    string? ErrorDetailsSummary,
    string? MercadoPagoRequestId);
