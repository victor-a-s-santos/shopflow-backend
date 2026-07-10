using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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

        var payload = new MercadoPagoCreateOrderRequest
        {
            Type = "online",
            ExternalReference = externalReference,
            TotalAmount = amount,
            ProcessingMode = "automatic",
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
            Content = JsonContent.Create(payload)
        };

        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);
        httpRequest.Headers.Add("X-Idempotency-Key", idempotencyKey);

        _logger.LogInformation(
            "Creating Mercado Pago Pix order for Shopflow order {OrderId} amount {Amount}",
            request.OrderId,
            request.Amount);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var providerMessage = TryExtractErrorMessage(responseBody);
            _logger.LogError(
                "Mercado Pago Pix order failed for Shopflow order {OrderId}. Status={StatusCode} Message={Message}",
                request.OrderId,
                (int)response.StatusCode,
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

    private static string TryExtractErrorMessage(string responseBody)
    {
        try
        {
            var error = JsonSerializer.Deserialize<MercadoPagoErrorResponse>(responseBody, JsonOptions);
            var causeDescription = error?.Cause?.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c.Description))?.Description;
            var apiError = error?.Errors?.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.Message))?.Message;

            if (causeDescription?.Contains("Unauthorized use of live credentials", StringComparison.OrdinalIgnoreCase) == true
                || error?.Message?.Contains("Unauthorized use of live credentials", StringComparison.OrdinalIgnoreCase) == true)
            {
                return "Unauthorized use of live credentials. Use the Access Token from Mercado Pago *Credenciais de teste* (Checkout API /v1/orders).";
            }

            if (!string.IsNullOrWhiteSpace(error?.Message))
                return error.Message;

            if (apiError is not null)
                return apiError;

            if (causeDescription is not null)
                return causeDescription;

            return string.IsNullOrWhiteSpace(responseBody) ? "Unknown Mercado Pago error." : responseBody;
        }
        catch
        {
            return string.IsNullOrWhiteSpace(responseBody) ? "Unknown Mercado Pago error." : responseBody;
        }
    }
}
