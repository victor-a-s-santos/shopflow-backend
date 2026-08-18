using MediatR;
using Vls.Shopflow.IdentityAccess.Application.Interfaces;
using Vls.Shopflow.IdentityAccess.Domain.Constants;
using Vls.Shopflow.Orders.Application.Commands;
using Vls.Shopflow.Orders.Application.Queries;

namespace Vls.Shopflow.HttpApi.Endpoints;

public static class AdminDeliveryBatchesEndpoints
{
    public static RouteGroupBuilder MapAdminDeliveryBatchesEndpoints(this RouteGroupBuilder group)
    {
        var batches = group.MapGroup("/admin/delivery-batches")
            .WithTags("AdminDeliveryBatches")
            .RequireAuthorization(AuthPolicies.Backoffice);

        batches.MapGet("", async (
            ISender sender,
            int? page,
            int? pageSize,
            string? status,
            string? q,
            string? customerEmail,
            DateTimeOffset? createdFrom,
            DateTimeOffset? createdTo,
            string? sort,
            CancellationToken ct) =>
        {
            var result = await sender.Send(
                new GetDeliveryBatchesQuery(
                    page ?? 1,
                    pageSize ?? 20,
                    status,
                    q,
                    customerEmail,
                    createdFrom,
                    createdTo,
                    sort),
                ct);
            return Results.Ok(result);
        });

        batches.MapGet("/{batchId:guid}", async (
            ISender sender,
            Guid batchId,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetDeliveryBatchByIdQuery(batchId), ct);
            return Results.Ok(result);
        });

        batches.MapPost("", async (
            ISender sender,
            ICurrentAdminAccessor currentAdmin,
            CreateDeliveryBatchRequest req,
            CancellationToken ct) =>
        {
            var admin = await currentAdmin.GetCurrentAdminAsync(ct);
            if (admin is null)
                return Results.Unauthorized();

            var result = await sender.Send(
                new CreateDeliveryBatchCommand(
                    req.OrderIds ?? [],
                    admin.Id,
                    req.DeliveryMethod,
                    req.TrackingCode,
                    req.InternalNote,
                    req.ConfirmDifferentAddresses),
                ct);

            return Results.Created($"/api/admin/delivery-batches/{result.Id}", result);
        });

        batches.MapPost("/{batchId:guid}/ship", async (
            ISender sender,
            ICurrentAdminAccessor currentAdmin,
            Guid batchId,
            ShipDeliveryBatchRequest? req,
            CancellationToken ct) =>
        {
            var admin = await currentAdmin.GetCurrentAdminAsync(ct);
            if (admin is null)
                return Results.Unauthorized();

            var result = await sender.Send(
                new ShipDeliveryBatchCommand(
                    batchId,
                    admin.Id,
                    req?.DeliveryMethod,
                    req?.TrackingCode,
                    req?.InternalNote),
                ct);

            return Results.Ok(result);
        });

        batches.MapPost("/{batchId:guid}/deliver", async (
            ISender sender,
            ICurrentAdminAccessor currentAdmin,
            Guid batchId,
            DeliverDeliveryBatchRequest? req,
            CancellationToken ct) =>
        {
            var admin = await currentAdmin.GetCurrentAdminAsync(ct);
            if (admin is null)
                return Results.Unauthorized();

            var result = await sender.Send(
                new DeliverDeliveryBatchCommand(batchId, admin.Id, req?.InternalNote),
                ct);

            return Results.Ok(result);
        });

        batches.MapPut("/{batchId:guid}/internal-note", async (
            ISender sender,
            Guid batchId,
            UpdateDeliveryBatchInternalNoteRequest req,
            CancellationToken ct) =>
        {
            var result = await sender.Send(
                new UpdateDeliveryBatchInternalNoteCommand(batchId, req.InternalNote),
                ct);
            return Results.Ok(result);
        });

        return group;
    }
}

public sealed record CreateDeliveryBatchRequest(
    IReadOnlyList<Guid>? OrderIds,
    string? DeliveryMethod = null,
    string? TrackingCode = null,
    string? InternalNote = null,
    bool ConfirmDifferentAddresses = false);

public sealed record ShipDeliveryBatchRequest(
    string? DeliveryMethod = null,
    string? TrackingCode = null,
    string? InternalNote = null);

public sealed record DeliverDeliveryBatchRequest(string? InternalNote = null);

public sealed record UpdateDeliveryBatchInternalNoteRequest(string? InternalNote);
