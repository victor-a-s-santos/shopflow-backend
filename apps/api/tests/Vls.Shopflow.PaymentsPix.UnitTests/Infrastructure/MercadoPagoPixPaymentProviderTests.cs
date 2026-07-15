using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Vls.Shopflow.PaymentsPix.Application.Interfaces;
using Vls.Shopflow.PaymentsPix.Application.Options;
using Vls.Shopflow.PaymentsPix.Domain.Enums;
using Vls.Shopflow.PaymentsPix.Domain.Exceptions;
using Vls.Shopflow.PaymentsPix.Infrastructure.Providers;

namespace Vls.Shopflow.PaymentsPix.UnitTests.Infrastructure;

public sealed class MercadoPagoPixPaymentProviderTests
{
    [Fact]
    public async Task CreatePixChargeAsync_WithValidOrderResponse_ReturnsPendingWithQrData()
    {
        var orderId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var expiresAt = new DateTimeOffset(2026, 7, 9, 23, 30, 0, TimeSpan.FromHours(-3));

        var handler = new StubHttpMessageHandler((request, _) =>
        {
            request.Method.Should().Be(HttpMethod.Post);
            request.RequestUri!.AbsolutePath.Should().Be("/v1/orders");
            request.Headers.Authorization!.Parameter.Should().Be("test-access-token");
            request.Headers.GetValues("X-Idempotency-Key").Single().Should().Be(orderId.ToString("D"));

            var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            using var json = JsonDocument.Parse(body);
            json.RootElement.GetProperty("type").GetString().Should().Be("online");
            json.RootElement.GetProperty("total_amount").GetString().Should().Be("149.90");
            json.RootElement.GetProperty("external_reference").GetString().Should().Be(orderId.ToString("D"));

            var payment = json.RootElement
                .GetProperty("transactions")
                .GetProperty("payments")[0];
            payment.GetProperty("amount").GetString().Should().Be("149.90");
            payment.GetProperty("payment_method").GetProperty("id").GetString().Should().Be("pix");
            payment.GetProperty("payment_method").GetProperty("type").GetString().Should().Be("bank_transfer");

            var responseJson = """
                {
                  "id": "ORD01JP84C939T20S0P1DN382FQ6K",
                  "status": "action_required",
                  "status_detail": "waiting_transfer",
                  "transactions": {
                    "payments": [
                      {
                        "id": "PAY01JP84C939T20S0P1DN6FCMWQC",
                        "status": "action_required",
                        "status_detail": "waiting_transfer",
                        "date_of_expiration": "2026-07-09T23:30:00.000-03:00",
                        "payment_method": {
                          "id": "pix",
                          "type": "bank_transfer",
                          "qr_code": "00020126580014BR.GOV.BCB.PIX0136abc123",
                          "qr_code_base64": "iVBORw0KGgo=",
                          "ticket_url": "https://www.mercadopago.com.br/sandbox/payments/123/ticket"
                        }
                      }
                    ]
                  }
                }
                """;

            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        });

        var provider = CreateProvider(handler);

        var result = await provider.CreatePixChargeAsync(
            new PixChargeRequest(orderId, 149.90m, "Maria Silva", "maria@test.com", expiresAt),
            CancellationToken.None);

        result.Provider.Should().Be(PixPaymentProviderType.MercadoPago);
        result.ProviderOrderId.Should().Be("ORD01JP84C939T20S0P1DN382FQ6K");
        result.ProviderTransactionId.Should().Be("PAY01JP84C939T20S0P1DN6FCMWQC");
        result.Status.Should().Be(PixPaymentStatus.Pending);
        result.CopyPasteCode.Should().Be("00020126580014BR.GOV.BCB.PIX0136abc123");
        result.QrCode.Should().Be("data:image/png;base64,iVBORw0KGgo=");
        result.QrCodeImageUrl.Should().BeNull();
        result.TicketUrl.Should().Be("https://www.mercadopago.com.br/sandbox/payments/123/ticket");
        result.ProviderStatus.Should().Be("action_required");
        result.ProviderStatusDetail.Should().Be("waiting_transfer");
    }

    [Fact]
    public async Task CreatePixChargeAsync_WhenApiFails_ThrowsMercadoPagoPixChargeFailedException()
    {
        var orderId = Guid.NewGuid();
        var handler = new StubHttpMessageHandler((_, _) =>
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(
                    """{"message":"Invalid payer email","status":400}""",
                    Encoding.UTF8,
                    "application/json")
            });

        var provider = CreateProvider(handler);

        var act = () => provider.CreatePixChargeAsync(
            new PixChargeRequest(orderId, 10m, "João", "invalid", DateTimeOffset.UtcNow.AddMinutes(30)),
            CancellationToken.None);

        await act.Should().ThrowAsync<MercadoPagoPixChargeFailedException>()
            .Where(ex => ex.OrderId == orderId && ex.StatusCode == 400);
    }

    [Fact]
    public async Task CreatePixChargeAsync_WhenSendNotificationUrlFalse_OmitsNotificationUrl()
    {
        string? requestBody = null;
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            requestBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return SuccessOrderResponse();
        });

        var provider = CreateProvider(
            handler,
            new MercadoPagoOptions
            {
                AccessToken = "test-access-token",
                NotificationUrl = "https://api-teste.example/api/payments/pix/webhooks/mercado-pago",
                SendNotificationUrlInOrderCreate = false
            });

        await provider.CreatePixChargeAsync(
            new PixChargeRequest(Guid.NewGuid(), 10m, "João", "a@b.com", DateTimeOffset.UtcNow.AddMinutes(30)),
            CancellationToken.None);

        using var json = JsonDocument.Parse(requestBody!);
        json.RootElement.TryGetProperty("notification_url", out _).Should().BeFalse();
    }

    [Fact]
    public async Task CreatePixChargeAsync_WhenSendNotificationUrlTrue_IncludesNotificationUrl()
    {
        const string notificationUrl = "https://api-teste.example/api/payments/pix/webhooks/mercado-pago";
        string? requestBody = null;
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            requestBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return SuccessOrderResponse();
        });

        var provider = CreateProvider(
            handler,
            new MercadoPagoOptions
            {
                AccessToken = "test-access-token",
                NotificationUrl = notificationUrl,
                SendNotificationUrlInOrderCreate = true
            });

        await provider.CreatePixChargeAsync(
            new PixChargeRequest(Guid.NewGuid(), 10m, "João", "a@b.com", DateTimeOffset.UtcNow.AddMinutes(30)),
            CancellationToken.None);

        using var json = JsonDocument.Parse(requestBody!);
        json.RootElement.GetProperty("notification_url").GetString().Should().Be(notificationUrl);
    }

    [Fact]
    public async Task CreatePixChargeAsync_WhenAccessTokenMissing_Throws()
    {
        var handler = new StubHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK));
        var provider = new MercadoPagoPixPaymentProvider(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.mercadopago.com/") },
            Options.Create(new MercadoPagoOptions()),
            NullLogger<MercadoPagoPixPaymentProvider>.Instance);

        var act = () => provider.CreatePixChargeAsync(
            new PixChargeRequest(Guid.NewGuid(), 10m, "João", "a@b.com", DateTimeOffset.UtcNow.AddMinutes(30)),
            CancellationToken.None);

        await act.Should().ThrowAsync<MercadoPagoPixChargeFailedException>();
    }

    [Theory]
    [InlineData("João Silva", "João", "Silva")]
    [InlineData("Maria", "Maria", "Shopflow")]
    [InlineData("", "Cliente", "Shopflow")]
    public void SplitCustomerName_SplitsAsExpected(string input, string first, string last)
    {
        var result = MercadoPagoPixPaymentProvider.SplitCustomerName(input);
        result.FirstName.Should().Be(first);
        result.LastName.Should().Be(last);
    }

    [Fact]
    public void FormatAmount_UsesTwoDecimalPlaces()
    {
        MercadoPagoPixPaymentProvider.FormatAmount(59.9m).Should().Be("59.90");
        MercadoPagoPixPaymentProvider.FormatAmount(100m).Should().Be("100.00");
    }

    private static MercadoPagoPixPaymentProvider CreateProvider(
        HttpMessageHandler handler,
        MercadoPagoOptions? options = null)
    {
        return new MercadoPagoPixPaymentProvider(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.mercadopago.com/") },
            Options.Create(options ?? new MercadoPagoOptions { AccessToken = "test-access-token" }),
            NullLogger<MercadoPagoPixPaymentProvider>.Instance);
    }

    private static HttpResponseMessage SuccessOrderResponse()
    {
        const string responseJson = """
            {
              "id": "ORD01JP84C939T20S0P1DN382FQ6K",
              "status": "action_required",
              "status_detail": "waiting_transfer",
              "transactions": {
                "payments": [
                  {
                    "id": "PAY01JP84C939T20S0P1DN6FCMWQC",
                    "status": "action_required",
                    "status_detail": "waiting_transfer",
                    "payment_method": {
                      "id": "pix",
                      "type": "bank_transfer",
                      "qr_code": "00020126580014BR.GOV.BCB.PIX0136abc123"
                    }
                  }
                ]
              }
            }
            """;

        return new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        };
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(handler(request, cancellationToken));
    }
}
