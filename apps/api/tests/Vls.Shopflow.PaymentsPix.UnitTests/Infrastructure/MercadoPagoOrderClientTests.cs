using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Vls.Shopflow.PaymentsPix.Application.Interfaces;
using Vls.Shopflow.PaymentsPix.Application.Options;
using Vls.Shopflow.PaymentsPix.Infrastructure.MercadoPago;

namespace Vls.Shopflow.PaymentsPix.UnitTests.Infrastructure;

public sealed class MercadoPagoOrderClientTests
{
    [Fact]
    public async Task GetOrderAsync_CallsOrdersEndpointWithBearerToken()
    {
        HttpRequestMessage? captured = null;
        var handler = new StubHandler((request, _) =>
        {
            captured = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "id": "ORD01JP84C939T20S0P1DN382FQ6K",
                      "status": "processed",
                      "status_detail": "accredited",
                      "external_reference": "11111111-1111-1111-1111-111111111111",
                      "total_amount": "59.90",
                      "transactions": {
                        "payments": [
                          {
                            "id": "PAY01JP84C939T20S0P1DN6FCMWQC",
                            "amount": "59.90",
                            "status": "processed",
                            "status_detail": "accredited",
                            "payment_method": {
                              "id": "pix",
                              "type": "bank_transfer"
                            }
                          }
                        ]
                      }
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            };
        });

        var client = new MercadoPagoOrderClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.mercadopago.com/") },
            Options.Create(new MercadoPagoOptions { AccessToken = "token-abc" }),
            NullLogger<MercadoPagoOrderClient>.Instance);

        var result = await client.GetOrderAsync("ORD01JP84C939T20S0P1DN382FQ6K", CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Method.Should().Be(HttpMethod.Get);
        captured.RequestUri!.AbsolutePath.Should().Be("/v1/orders/ORD01JP84C939T20S0P1DN382FQ6K");
        captured.Headers.Authorization!.Parameter.Should().Be("token-abc");

        result.Status.Should().Be(MercadoPagoOrderLookupStatus.Found);
        result.Order.Should().NotBeNull();
        result.Order!.Status.Should().Be("processed");
        result.Order.StatusDetail.Should().Be("accredited");
        result.Order.PaymentMethodId.Should().Be("pix");
        result.Order.TotalAmount.Should().Be(59.90m);
        result.Order.TransactionId.Should().Be("PAY01JP84C939T20S0P1DN6FCMWQC");
    }

    [Fact]
    public async Task GetOrderAsync_WhenNotFound_ReturnsNotFoundStatus()
    {
        var handler = new StubHandler((_, _) => new HttpResponseMessage(HttpStatusCode.NotFound));
        var client = new MercadoPagoOrderClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.mercadopago.com/") },
            Options.Create(new MercadoPagoOptions { AccessToken = "token-abc" }),
            NullLogger<MercadoPagoOrderClient>.Instance);

        var result = await client.GetOrderAsync("ORD01MISSING", CancellationToken.None);
        result.Status.Should().Be(MercadoPagoOrderLookupStatus.NotFound);
        result.Order.Should().BeNull();
        result.HttpStatusCode.Should().Be(404);
    }

    [Fact]
    public async Task GetOrderAsync_WhenBadRequest_ReturnsBadRequestStatus_DoesNotThrow()
    {
        var handler = new StubHandler((_, _) => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""{"message":"invalid id"}""", Encoding.UTF8, "application/json")
        });
        var client = new MercadoPagoOrderClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.mercadopago.com/") },
            Options.Create(new MercadoPagoOptions { AccessToken = "token-abc" }),
            NullLogger<MercadoPagoOrderClient>.Instance);

        var result = await client.GetOrderAsync("123456", CancellationToken.None);
        result.Status.Should().Be(MercadoPagoOrderLookupStatus.BadRequest);
        result.Order.Should().BeNull();
        result.HttpStatusCode.Should().Be(400);
    }

    [Fact]
    public async Task GetOrderAsync_WhenUnauthorized_ReturnsUnauthorizedStatus()
    {
        var handler = new StubHandler((_, _) => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var client = new MercadoPagoOrderClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.mercadopago.com/") },
            Options.Create(new MercadoPagoOptions { AccessToken = "bad-token" }),
            NullLogger<MercadoPagoOrderClient>.Instance);

        var result = await client.GetOrderAsync("ORD01X", CancellationToken.None);
        result.Status.Should().Be(MercadoPagoOrderLookupStatus.Unauthorized);
        result.HttpStatusCode.Should().Be(401);
    }

    [Fact]
    public async Task GetOrderAsync_WhenServerError_ReturnsTransientFailure()
    {
        var handler = new StubHandler((_, _) => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var client = new MercadoPagoOrderClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.mercadopago.com/") },
            Options.Create(new MercadoPagoOptions { AccessToken = "token-abc" }),
            NullLogger<MercadoPagoOrderClient>.Instance);

        var result = await client.GetOrderAsync("ORD01X", CancellationToken.None);
        result.Status.Should().Be(MercadoPagoOrderLookupStatus.TransientFailure);
        result.HttpStatusCode.Should().Be(500);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(handler(request, cancellationToken));
    }
}
