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

    public async Task<MercadoPagoOrderLookup?> GetOrderAsync(
        string orderId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.AccessToken))
            throw new InvalidOperationException("Mercado Pago Access Token is not configured.");

        using var request = new HttpRequestMessage(HttpMethod.Get, $"v1/orders/{orderId.Trim()}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);
        request.Headers.Accept.ParseAdd("application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            _logger.LogError(
                "Mercado Pago GET /v1/orders/{OrderId} unauthorized/forbidden with status {StatusCode}.",
                orderId,
                (int)response.StatusCode);
            throw new HttpRequestException(
                $"Mercado Pago order lookup unauthorized with status {(int)response.StatusCode}.");
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Mercado Pago GET /v1/orders/{OrderId} failed with status {StatusCode}.",
                orderId,
                (int)response.StatusCode);
            throw new HttpRequestException(
                $"Mercado Pago order lookup failed with status {(int)response.StatusCode}.");
        }

        var dto = JsonSerializer.Deserialize<MercadoPagoOrderResponse>(body, JsonOptions)
                  ?? throw new InvalidOperationException("Mercado Pago returned an empty order payload.");

        var payment = dto.Transactions?.Payments?.FirstOrDefault();
        var totalAmount = ParseAmount(dto.TotalAmount) ?? ParseAmount(payment?.Amount) ?? 0m;

        return new MercadoPagoOrderLookup(
            dto.Id ?? orderId,
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
