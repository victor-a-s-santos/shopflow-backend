using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vls.Shopflow.PaymentsPix.Application.Interfaces;
using Vls.Shopflow.PaymentsPix.Application.Options;

namespace Vls.Shopflow.PaymentsPix.Infrastructure.MercadoPago;

public sealed class MercadoPagoOrderClient : IMercadoPagoOrderClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly MercadoPagoOptions _options;
    private readonly ILogger<MercadoPagoOrderClient> _logger;

    public MercadoPagoOrderClient(
        HttpClient httpClient,
        IOptions<MercadoPagoOptions> options,
        ILogger<MercadoPagoOrderClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<MercadoPagoOrderLookupResult> GetOrderAsync(
        string orderId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.AccessToken))
            throw new InvalidOperationException("Mercado Pago Access Token is not configured.");

        var trimmedId = orderId.Trim();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"v1/orders/{trimmedId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);
        request.Headers.Accept.ParseAdd("application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var statusCode = (int)response.StatusCode;

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning(
                "Mercado Pago GET /v1/orders lookup returned 404. ProviderOrderId={MaskedId}",
                MaskOrderId(trimmedId));
            return new MercadoPagoOrderLookupResult(
                MercadoPagoOrderLookupStatus.NotFound,
                null,
                statusCode,
                "Order not found at Mercado Pago.");
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            _logger.LogWarning(
                "Mercado Pago GET /v1/orders lookup returned 400 (invalid id or payload). ProviderOrderId={MaskedId}",
                MaskOrderId(trimmedId));
            return new MercadoPagoOrderLookupResult(
                MercadoPagoOrderLookupStatus.BadRequest,
                null,
                statusCode,
                "Mercado Pago rejected order id (bad request).");
        }

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            _logger.LogError(
                "Mercado Pago GET /v1/orders unauthorized/forbidden with status {StatusCode}. Check AccessToken config.",
                statusCode);
            return new MercadoPagoOrderLookupResult(
                MercadoPagoOrderLookupStatus.Unauthorized,
                null,
                statusCode,
                $"Mercado Pago order lookup unauthorized with status {statusCode}.");
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Mercado Pago GET /v1/orders transient/failed with status {StatusCode}. ProviderOrderId={MaskedId}",
                statusCode,
                MaskOrderId(trimmedId));
            return new MercadoPagoOrderLookupResult(
                MercadoPagoOrderLookupStatus.TransientFailure,
                null,
                statusCode,
                $"Mercado Pago order lookup failed with status {statusCode}.");
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var dto = JsonSerializer.Deserialize<MercadoPagoOrderResponse>(body, JsonOptions)
                  ?? throw new InvalidOperationException("Mercado Pago returned an empty order payload.");

        var payment = dto.Transactions?.Payments?.FirstOrDefault();
        var totalAmount = ParseAmount(dto.TotalAmount) ?? ParseAmount(payment?.Amount) ?? 0m;

        var order = new MercadoPagoOrderLookup(
            dto.Id ?? trimmedId,
            dto.Status ?? string.Empty,
            dto.StatusDetail,
            dto.ExternalReference,
            totalAmount,
            payment?.Id,
            ParseAmount(payment?.Amount),
            payment?.Status,
            payment?.StatusDetail,
            payment?.PaymentMethod?.Id,
            payment?.PaymentMethod?.Type,
            payment?.PaymentMethod?.QrCode,
            payment?.PaymentMethod?.QrCodeBase64,
            payment?.PaymentMethod?.TicketUrl,
            dto.LastUpdatedDate,
            dto.CreatedDate);

        return new MercadoPagoOrderLookupResult(MercadoPagoOrderLookupStatus.Found, order, statusCode, null);
    }

    internal static string MaskOrderId(string orderId)
    {
        var trimmed = orderId.Trim();
        if (trimmed.Length <= 8)
            return "***";

        return $"{trimmed[..4]}…{trimmed[^4..]}";
    }

    private static decimal? ParseAmount(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
            ? amount
            : null;
    }
}
