using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vls.Shopflow.PaymentsPix.Application.Interfaces;
using Vls.Shopflow.PaymentsPix.Application.Options;
using Vls.Shopflow.PaymentsPix.Domain.Enums;
using Vls.Shopflow.PaymentsPix.Domain.Exceptions;
using Vls.Shopflow.PaymentsPix.Infrastructure.MercadoPago;

namespace Vls.Shopflow.PaymentsPix.Infrastructure.Providers;

public sealed class MercadoPagoPixPaymentProvider : IPixPaymentProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions RequestJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly MercadoPagoOptions _options;
    private readonly ILogger<MercadoPagoPixPaymentProvider> _logger;

    public MercadoPagoPixPaymentProvider(
        HttpClient httpClient,
        IOptions<MercadoPagoOptions> options,
        ILogger<MercadoPagoPixPaymentProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PixChargeResponse> CreatePixChargeAsync(
        PixChargeRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.AccessToken))
        {
            throw new MercadoPagoPixChargeFailedException(
                request.OrderId,
                null,
                null,
                "Mercado Pago Access Token is not configured (MercadoPago:AccessToken).");
        }

        var (firstName, lastName) = SplitCustomerName(request.CustomerName);
        if (_options.IsSandbox && !string.IsNullOrWhiteSpace(_options.SandboxPayerFirstNameOverride))
            firstName = _options.SandboxPayerFirstNameOverride.Trim();

        var payerEmail = request.CustomerEmail;
        if (_options.IsSandbox
            && !string.IsNullOrWhiteSpace(_options.SandboxTestPayerEmail)
            && string.Equals(firstName, "APRO", StringComparison.OrdinalIgnoreCase))
        {
            payerEmail = _options.SandboxTestPayerEmail;
        }

        var amount = FormatAmount(request.Amount);
        var externalReference = request.OrderId.ToString("D");
        var idempotencyKey = externalReference;

        var notificationUrlConfigured = !string.IsNullOrWhiteSpace(_options.NotificationUrl);
        var notificationUrlSent = _options.SendNotificationUrlInOrderCreate && notificationUrlConfigured;

        var payload = new MercadoPagoCreateOrderRequest
        {
            Type = "online",
            ExternalReference = externalReference,
            TotalAmount = amount,
            ProcessingMode = "automatic",
            NotificationUrl = notificationUrlSent ? _options.NotificationUrl.Trim() : null,
            Payer = new MercadoPagoOrderPayerRequest
            {
                Email = payerEmail,
                FirstName = firstName,
                LastName = lastName
            },
            Transactions = new MercadoPagoOrderTransactionsRequest
            {
                Payments =
                [
                    new MercadoPagoOrderPaymentRequest
                    {
                        Amount = amount,
                        ExpirationTime = FormatExpirationDuration(request.ExpiresAt),
                        PaymentMethod = new MercadoPagoOrderPaymentMethodRequest
                        {
                            Id = "pix",
                            Type = "bank_transfer"
                        }
                    }
                ]
            }
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "v1/orders")
        {
            Content = JsonContent.Create(payload, options: RequestJsonOptions)
        };

        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);
        httpRequest.Headers.Add("X-Idempotency-Key", idempotencyKey);

        _logger.LogInformation(
            "Creating Mercado Pago Pix order for Shopflow order {OrderId} amount {Amount}. " +
            "MercadoPago notification_url sent: {NotificationUrlSent}. NotificationUrl configured: {NotificationUrlConfigured}.",
            request.OrderId,
            request.Amount,
            notificationUrlSent,
            notificationUrlConfigured);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var mpRequestId = TryGetMercadoPagoRequestId(response);
            var failure = TryParseCreateOrderFailure(
                (int)response.StatusCode,
                responseBody,
                mpRequestId);
            var providerMessage = failure.ProviderMessage
                                  ?? (string.IsNullOrWhiteSpace(responseBody)
                                      ? "Unknown Mercado Pago error."
                                      : "Mercado Pago returned a non-success response.");

            _logger.LogError(
                "Mercado Pago Pix order failed for Shopflow order {OrderId}. " +
                "HttpStatus={HttpStatus} MpRequestId={MpRequestId} MpOrderId={MpOrderId} " +
                "OrderStatus={OrderStatus} OrderStatusDetail={OrderStatusDetail} " +
                "TransactionId={TransactionId} TransactionStatus={TransactionStatus} " +
                "TransactionStatusDetail={TransactionStatusDetail} " +
                "Error={Error} ErrorCode={ErrorCode} CauseCode={CauseCode} " +
                "CauseDescription={CauseDescription} ErrorDetails={ErrorDetails} Message={Message}",
                request.OrderId,
                failure.HttpStatusCode,
                failure.MercadoPagoRequestId,
                failure.ProviderOrderId,
                failure.OrderStatus,
                failure.OrderStatusDetail,
                failure.TransactionId,
                failure.TransactionStatus,
                failure.TransactionStatusDetail,
                failure.Error,
                failure.ErrorCode,
                failure.CauseCode,
                failure.CauseDescription,
                failure.ErrorDetailsSummary,
                providerMessage);

            throw new MercadoPagoPixChargeFailedException(
                request.OrderId,
                (int)response.StatusCode,
                providerMessage,
                $"Mercado Pago Pix charge failed for order {request.OrderId}: {providerMessage}");
        }

        var order = JsonSerializer.Deserialize<MercadoPagoOrderResponse>(responseBody, JsonOptions)
                    ?? throw new MercadoPagoPixChargeFailedException(
                        request.OrderId,
                        (int)response.StatusCode,
                        responseBody,
                        "Mercado Pago returned an empty order response.");

        var payment = order.Transactions?.Payments?.FirstOrDefault()
                      ?? throw new MercadoPagoPixChargeFailedException(
                          request.OrderId,
                          (int)response.StatusCode,
                          responseBody,
                          "Mercado Pago order response did not include a payment transaction.");

        var paymentMethod = payment.PaymentMethod;
        var copyPasteCode = paymentMethod?.QrCode;
        var ticketUrl = paymentMethod?.TicketUrl;
        var qrCodeBase64 = paymentMethod?.QrCodeBase64;

        if (string.IsNullOrWhiteSpace(copyPasteCode))
        {
            throw new MercadoPagoPixChargeFailedException(
                request.OrderId,
                (int)response.StatusCode,
                responseBody,
                "Mercado Pago Pix response did not include qr_code (copia e cola).");
        }

        var providerOrderId = order.Id
                              ?? throw new MercadoPagoPixChargeFailedException(
                                  request.OrderId,
                                  (int)response.StatusCode,
                                  responseBody,
                                  "Mercado Pago order response did not include order id (ORD...).");

        var providerTransactionId = payment.Id
                                    ?? throw new MercadoPagoPixChargeFailedException(
                                        request.OrderId,
                                        (int)response.StatusCode,
                                        responseBody,
                                        "Mercado Pago order response did not include transaction id (PAY...).");

        _logger.LogInformation(
            "Mercado Pago Pix order created for Shopflow order {OrderId}. MpOrderId={MpOrderId} MpPaymentId={MpPaymentId} Status={Status}",
            request.OrderId,
            providerOrderId,
            providerTransactionId,
            order.Status);

        return new PixChargeResponse(
            PixPaymentProviderType.MercadoPago,
            ProviderOrderId: providerOrderId,
            ProviderTransactionId: providerTransactionId,
            QrCode: FormatQrCodeBase64(qrCodeBase64),
            QrCodeImageUrl: null,
            CopyPasteCode: copyPasteCode,
            TicketUrl: ticketUrl,
            ProviderStatus: order.Status,
            ProviderStatusDetail: order.StatusDetail,
            ProviderTransactionStatus: payment.Status,
            ProviderTransactionStatusDetail: payment.StatusDetail,
            ExternalReference: externalReference,
            IdempotencyKey: idempotencyKey,
            ExpiresAt: payment.DateOfExpiration ?? request.ExpiresAt,
            Status: PixPaymentStatus.Pending);
    }

    internal static string? FormatQrCodeBase64(string? qrCodeBase64)
    {
        if (string.IsNullOrWhiteSpace(qrCodeBase64))
            return null;

        var value = qrCodeBase64.Trim();
        if (value.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return value;

        return $"data:image/png;base64,{value}";
    }

    internal static (string FirstName, string LastName) SplitCustomerName(string fullName)
    {
        var trimmed = fullName.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return ("Cliente", "Shopflow");

        var parts = trimmed.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            1 => (parts[0], "Shopflow"),
            _ => (parts[0], parts[1])
        };
    }

    internal static string FormatAmount(decimal amount)
        => amount.ToString("F2", CultureInfo.InvariantCulture);

    internal static string FormatExpirationDuration(DateTimeOffset expiresAt)
    {
        var duration = expiresAt - DateTimeOffset.UtcNow;
        if (duration <= TimeSpan.Zero)
            duration = TimeSpan.FromMinutes(30);

        return $"PT{(int)duration.TotalMinutes}M";
    }

    internal static MercadoPagoCreateOrderFailureDetails TryParseCreateOrderFailure(
        int httpStatusCode,
        string responseBody,
        string? mercadoPagoRequestId)
    {
        try
        {
            var error = JsonSerializer.Deserialize<MercadoPagoErrorResponse>(responseBody, JsonOptions);
            if (error is null)
            {
                return new MercadoPagoCreateOrderFailureDetails(
                    httpStatusCode,
                    ProviderMessage: string.IsNullOrWhiteSpace(responseBody)
                        ? "Unknown Mercado Pago error."
                        : "Mercado Pago returned a non-success response.",
                    Error: null,
                    ProviderOrderId: null,
                    OrderStatus: null,
                    OrderStatusDetail: null,
                    TransactionId: null,
                    TransactionStatus: null,
                    TransactionStatusDetail: null,
                    ErrorCode: null,
                    CauseCode: null,
                    CauseDescription: null,
                    ErrorDetailsSummary: null,
                    MercadoPagoRequestId: mercadoPagoRequestId);
            }

            var firstCause = error.Cause?.FirstOrDefault(c =>
                !string.IsNullOrWhiteSpace(c.Description) || !string.IsNullOrWhiteSpace(c.Code));
            var firstApiError = error.Errors?.FirstOrDefault(e =>
                !string.IsNullOrWhiteSpace(e.Message) || !string.IsNullOrWhiteSpace(e.Code));

            var orderId = NullIfWhiteSpace(error.Data?.Id) ?? NullIfWhiteSpace(error.Id);
            // Nested data.status, or flat string "status" on order-shaped bodies (int "status" is HTTP code).
            var orderStatus = NullIfWhiteSpace(error.Data?.Status)
                              ?? TryReadFlatOrderStatus(responseBody);
            var orderStatusDetail = NullIfWhiteSpace(error.Data?.StatusDetail)
                                    ?? NullIfWhiteSpace(error.StatusDetail);

            var payment = error.Data?.Transactions?.Payments?.FirstOrDefault()
                          ?? error.Transactions?.Payments?.FirstOrDefault();

            var detailsSummary = SummarizeErrorDetails(firstApiError?.Details);
            var providerMessage = BuildProviderMessage(error, firstApiError, firstCause, payment);

            return new MercadoPagoCreateOrderFailureDetails(
                httpStatusCode,
                ProviderMessage: providerMessage,
                Error: NullIfWhiteSpace(error.Error),
                ProviderOrderId: orderId,
                OrderStatus: orderStatus,
                OrderStatusDetail: orderStatusDetail,
                TransactionId: NullIfWhiteSpace(payment?.Id),
                TransactionStatus: NullIfWhiteSpace(payment?.Status),
                TransactionStatusDetail: NullIfWhiteSpace(payment?.StatusDetail),
                ErrorCode: NullIfWhiteSpace(firstApiError?.Code),
                CauseCode: NullIfWhiteSpace(firstCause?.Code),
                CauseDescription: NullIfWhiteSpace(firstCause?.Description),
                ErrorDetailsSummary: detailsSummary,
                MercadoPagoRequestId: mercadoPagoRequestId);
        }
        catch
        {
            return new MercadoPagoCreateOrderFailureDetails(
                httpStatusCode,
                ProviderMessage: string.IsNullOrWhiteSpace(responseBody)
                    ? "Unknown Mercado Pago error."
                    : "Mercado Pago returned a non-success response.",
                Error: null,
                ProviderOrderId: null,
                OrderStatus: null,
                OrderStatusDetail: null,
                TransactionId: null,
                TransactionStatus: null,
                TransactionStatusDetail: null,
                ErrorCode: null,
                CauseCode: null,
                CauseDescription: null,
                ErrorDetailsSummary: null,
                MercadoPagoRequestId: mercadoPagoRequestId);
        }
    }

    private static string BuildProviderMessage(
        MercadoPagoErrorResponse error,
        MercadoPagoApiError? firstApiError,
        MercadoPagoErrorCause? firstCause,
        MercadoPagoOrderPaymentResponse? payment)
    {
        var causeDescription = firstCause?.Description;
        var apiError = firstApiError?.Message;

        if (causeDescription?.Contains("Unauthorized use of live credentials", StringComparison.OrdinalIgnoreCase) == true
            || error.Message?.Contains("Unauthorized use of live credentials", StringComparison.OrdinalIgnoreCase) == true)
        {
            return "Unauthorized use of live credentials. Use the Access Token from Mercado Pago *Credenciais de teste* (Checkout API /v1/orders).";
        }

        if (!string.IsNullOrWhiteSpace(error.Message))
            return AppendStatusDetailHint(error.Message, payment?.StatusDetail, firstApiError?.Details);

        if (!string.IsNullOrWhiteSpace(apiError))
            return AppendStatusDetailHint(apiError, payment?.StatusDetail, firstApiError?.Details);

        if (!string.IsNullOrWhiteSpace(causeDescription))
            return causeDescription;

        if (!string.IsNullOrWhiteSpace(payment?.StatusDetail))
            return $"Transaction failed: {payment.StatusDetail}";

        return "Unknown Mercado Pago error.";
    }

    private static string AppendStatusDetailHint(
        string message,
        string? transactionStatusDetail,
        string[]? details)
    {
        if (!string.IsNullOrWhiteSpace(transactionStatusDetail)
            && !message.Contains(transactionStatusDetail, StringComparison.OrdinalIgnoreCase))
        {
            return $"{message} (status_detail={transactionStatusDetail})";
        }

        var detail = details?.FirstOrDefault(d => !string.IsNullOrWhiteSpace(d));
        if (!string.IsNullOrWhiteSpace(detail) && !message.Contains(detail, StringComparison.OrdinalIgnoreCase))
            return $"{message} ({detail})";

        return message;
    }

    private static string? SummarizeErrorDetails(string[]? details)
    {
        if (details is null || details.Length == 0)
            return null;

        // Keep short, non-sensitive summaries (e.g. "pay_…: high_risk") — never log payer/docs/QR.
        var parts = details
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Select(d => d.Trim())
            .Take(3)
            .ToArray();

        return parts.Length == 0 ? null : string.Join("; ", parts);
    }

    private static string? TryReadFlatOrderStatus(string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (doc.RootElement.TryGetProperty("status", out var statusEl)
                && statusEl.ValueKind == JsonValueKind.String)
            {
                return NullIfWhiteSpace(statusEl.GetString());
            }

            if (doc.RootElement.TryGetProperty("data", out var data)
                && data.ValueKind == JsonValueKind.Object
                && data.TryGetProperty("status", out var nestedStatus)
                && nestedStatus.ValueKind == JsonValueKind.String)
            {
                return NullIfWhiteSpace(nestedStatus.GetString());
            }
        }
        catch (JsonException)
        {
            // ignore
        }

        return null;
    }

    private static string? TryGetMercadoPagoRequestId(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("x-request-id", out var values))
            return NullIfWhiteSpace(values.FirstOrDefault());

        if (response.Headers.TryGetValues("X-Request-Id", out values))
            return NullIfWhiteSpace(values.FirstOrDefault());

        return null;
    }

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
