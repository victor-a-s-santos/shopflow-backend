using MediatR;
using Vls.Shopflow.IdentityAccess.Application.Interfaces;
using Vls.Shopflow.IdentityAccess.Domain.Constants;
using Vls.Shopflow.Orders.Application.Commands;
using Vls.Shopflow.Orders.Application.Queries;

namespace Vls.Shopflow.HttpApi.Endpoints;

public static class AdminOrdersEndpoints
{
    public static RouteGroupBuilder MapAdminOrdersEndpoints(this RouteGroupBuilder group)
    {
        var adminOrders = group.MapGroup("/admin/orders")
            .WithTags("AdminOrders")
            .RequireAuthorization(AuthPolicies.Backoffice);

        adminOrders.MapGet("", async (
            ISender sender,
            int? page,
            int? pageSize,
            string? status,
            string? paymentStatus,
            string? q,
            DateTimeOffset? createdFrom,
            DateTimeOffset? createdTo,
            bool? paidOnly,
            string? sort,
            string? fulfillmentStatus,
            CancellationToken ct) =>
        {
            var result = await sender.Send(
                new GetAdminOrdersQuery(
                    page ?? 1,
                    pageSize ?? 20,
                    status,
                    paymentStatus,
                    q,
                    createdFrom,
                    createdTo,
                    paidOnly,
                    sort,
                    fulfillmentStatus),
                ct);

            return Results.Ok(result);
        });

        adminOrders.MapGet("/{orderId:guid}", async (
            ISender sender,
            Guid orderId,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetAdminOrderByIdQuery(orderId), ct);
            return Results.Ok(result);
        });

        adminOrders.MapPost("/{orderId:guid}/fulfillment/ship", async (
            ISender sender,
            ICurrentAdminAccessor currentAdmin,
            Guid orderId,
            ShipOrderFulfillmentRequest req,
            CancellationToken ct) =>
        {
            var admin = await currentAdmin.GetCurrentAdminAsync(ct);
            if (admin is null)
                return Results.Unauthorized();

            var result = await sender.Send(
                new ShipOrderFulfillmentCommand(
                    orderId,
                    admin.Id,
                    req.FinalDeliveryMethod,
                    req.TrackingCode,
                    req.InternalNote),
                ct);

            return Results.Ok(result);
        });

        adminOrders.MapPost("/{orderId:guid}/fulfillment/deliver", async (
            ISender sender,
            ICurrentAdminAccessor currentAdmin,
            Guid orderId,
            DeliverOrderFulfillmentRequest? req,
            CancellationToken ct) =>
        {
            var admin = await currentAdmin.GetCurrentAdminAsync(ct);
            if (admin is null)
                return Results.Unauthorized();

            var result = await sender.Send(
                new DeliverOrderFulfillmentCommand(
                    orderId,
                    admin.Id,
                    req?.InternalNote),
                ct);

            return Results.Ok(result);
        });

        adminOrders.MapPut("/{orderId:guid}/internal-note", async (
            ISender sender,
            Guid orderId,
            UpdateOrderInternalNoteRequest req,
            CancellationToken ct) =>
        {
            var result = await sender.Send(
                new UpdateOrderInternalNoteCommand(orderId, req.InternalNote),
                ct);

            return Results.Ok(result);
        });

        return group;
    }
}

public sealed record ShipOrderFulfillmentRequest(
    string? FinalDeliveryMethod = null,
    string? TrackingCode = null,
    string? InternalNote = null);

public sealed record DeliverOrderFulfillmentRequest(string? InternalNote = null);

public sealed record UpdateOrderInternalNoteRequest(string? InternalNote);
