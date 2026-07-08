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
