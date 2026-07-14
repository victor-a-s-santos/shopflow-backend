using System.Text.Json;
using MediatR;
using Vls.Shopflow.IdentityAccess.Domain.Constants;
using Vls.Shopflow.PaymentsPix.Application.Commands;
using Vls.Shopflow.PaymentsPix.Application.Queries;

namespace Vls.Shopflow.HttpApi.Endpoints;

public static class PaymentsPixEndpoints
{
    public static RouteGroupBuilder MapPaymentsPixEndpoints(this RouteGroupBuilder group)
    {
        var payments = group.MapGroup("/payments/pix").WithTags("PaymentsPix");

        // Checkout MVP — create/get Pix for a valid order (idempotent POST).
        payments.MapPost("/orders/{orderId:guid}", async (
            ISender sender,
            Guid orderId,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new CreatePixPaymentForOrderCommand(orderId), ct);

            return result.WasCreated
                ? Results.Created($"/api/payments/pix/{result.Payment.PaymentId}", result.Payment)
                : Results.Ok(result.Payment);
        });

        payments.MapPost("/webhooks/mercado-pago", async (
            HttpRequest request,
            ISender sender,
            CancellationToken ct) =>
        {
            using var document = await JsonDocument.ParseAsync(request.Body, cancellationToken: ct);
            var root = document.RootElement;

            var dataIdFromQuery = request.Query["data.id"].FirstOrDefault();

            string? dataIdFromBody = null;
            if (root.TryGetProperty("data", out var dataElement)
                && dataElement.TryGetProperty("id", out var idElement))
            {
                dataIdFromBody = idElement.ValueKind == JsonValueKind.Number
                    ? idElement.GetRawText()
                    : idElement.GetString();
            }

            var providerEventId = root.TryGetProperty("id", out var eventIdElement)
                ? eventIdElement.ValueKind == JsonValueKind.Number
                    ? eventIdElement.GetRawText()
                    : eventIdElement.GetString()
                : null;

            var action = root.TryGetProperty("action", out var actionElement)
                ? actionElement.GetString()
                : null;

            var type = root.TryGetProperty("type", out var typeElement)
                ? typeElement.GetString()
                : null;

            var liveMode = root.TryGetProperty("live_mode", out var liveModeElement)
                           && liveModeElement.ValueKind is JsonValueKind.True;

            var result = await sender.Send(
                new ProcessMercadoPagoPixWebhookCommand(
                    dataIdFromQuery,
                    dataIdFromBody,
                    request.Headers["x-signature"].FirstOrDefault(),
                    request.Headers["x-request-id"].FirstOrDefault(),
                    action,
                    type,
                    liveMode,
                    providerEventId),
                ct);

            return Results.Json(
                new
                {
                    outcome = result.Outcome,
                    message = result.Message,
                    pixPaymentId = result.PixPaymentId,
                    orderId = result.OrderId
                },
                statusCode: result.StatusCode);
        });

        // Full payment details — backoffice only until guest access token exists (Phase 4).
        payments.MapGet("/{paymentId:guid}", async (
            ISender sender,
            Guid paymentId,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetPixPaymentByIdQuery(paymentId), ct);
            return Results.Ok(result);
        })
        .RequireAuthorization(AuthPolicies.Backoffice);

        payments.MapGet("/by-order/{orderId:guid}", async (
            ISender sender,
            Guid orderId,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetPixPaymentByOrderIdQuery(orderId), ct);
            return Results.Ok(result);
        })
        .RequireAuthorization(AuthPolicies.Backoffice);

        return group;
    }
}
