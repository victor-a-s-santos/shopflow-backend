using MediatR;
using Vls.Shopflow.IdentityAccess.Application.Interfaces;
using Vls.Shopflow.IdentityAccess.Domain.Constants;
using Vls.Shopflow.Inventory.Application.Commands;
using Vls.Shopflow.Inventory.Application.Queries;

namespace Vls.Shopflow.HttpApi.Endpoints;

public static class InventoryEndpoints
{
    public static RouteGroupBuilder MapInventoryEndpoints(this RouteGroupBuilder group)
    {
        var inv = group.MapGroup("/inventory").WithTags("Inventory");

        // Storefront: safe availability. Authenticated backoffice: full inventory breakdown.
        inv.MapGet("/skus/{skuId:guid}", async (
            HttpContext ctx,
            ISender sender,
            Guid skuId,
            CancellationToken ct) =>
        {
            var isBackoffice = ctx.User.Identity?.IsAuthenticated == true
                && ctx.User.IsInRole(AuthRoles.Owner)
                && string.Equals(
                    ctx.User.FindFirst(AuthClaims.IsStaff)?.Value,
                    "true",
                    StringComparison.OrdinalIgnoreCase);

            if (isBackoffice)
            {
                var full = await sender.Send(new GetInventoryBySkuIdQuery(skuId), ct);
                return full is null ? Results.NotFound() : Results.Ok(full);
            }

            var storeAccess = ctx.RequestServices.GetRequiredService<IStoreAccessPolicy>();
            if (storeAccess.RequireApprovedCustomerToBrowse)
            {
                var currentCustomer = ctx.RequestServices.GetRequiredService<ICurrentCustomerAccessor>();
                var customer = await currentCustomer.GetCurrentCustomerAsync(ct);
                var decision = storeAccess.EvaluateBrowse(customer);
                if (!decision.Allowed)
                    return StoreAccessHttp.Denied(ctx, decision);
            }

            var safe = await sender.Send(new GetSkuAvailabilityBySkuIdQuery(skuId), ct);
            return safe is null ? Results.NotFound() : Results.Ok(safe);
        });

        inv.MapGet("/skus/{skuId:guid}/movements", async (
            ISender sender,
            Guid skuId,
            int page = 1,
            int pageSize = 20,
            CancellationToken ct = default) =>
        {
            var dto = await sender.Send(new GetStockMovementsBySkuIdQuery(skuId, page, pageSize), ct);
            return Results.Ok(dto);
        })
        .RequireAuthorization(AuthPolicies.Backoffice);

        inv.MapPost("/skus/{skuId:guid}", async (
            ISender sender,
            Guid skuId,
            CreateInventoryForSkuRequest req,
            CancellationToken ct) =>
        {
            await sender.Send(new CreateInventoryItemCommand(skuId, req.InitialQuantity), ct);
            return Results.Created($"/api/inventory/skus/{skuId}", new { skuId });
        })
        .RequireAuthorization(AuthPolicies.Backoffice);

        inv.MapPost("/skus/{skuId:guid}/add", async (
            ISender sender,
            Guid skuId,
            StockChangeRequest req,
            CancellationToken ct) =>
        {
            await sender.Send(new AddStockCommand(skuId, req.Quantity, req.Reason), ct);
            return Results.NoContent();
        })
        .RequireAuthorization(AuthPolicies.Backoffice);

        inv.MapPost("/skus/{skuId:guid}/remove", async (
            ISender sender,
            Guid skuId,
            StockChangeRequest req,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new RemoveStockCommand(skuId, req.Quantity, req.Reason), ct);
            return Results.Ok(result);
        })
        .RequireAuthorization(AuthPolicies.Backoffice);

        MapInventoryAdminTechnicalEndpoints(group);
        return group;
    }

    /// <summary>
    /// Reservation lifecycle is internal (CartCheckout, Expiration worker).
    /// HTTP surface exists only for backoffice technical/debug use.
    /// </summary>
    private static void MapInventoryAdminTechnicalEndpoints(RouteGroupBuilder group)
    {
        var adminInv = group.MapGroup("/admin/inventory")
            .WithTags("InventoryAdmin")
            .RequireAuthorization(AuthPolicies.Backoffice);

        adminInv.MapGet("/skus", async (
            ISender sender,
            int? page,
            int? pageSize,
            string? sort,
            string? q,
            Guid? productId,
            string? categorySlug,
            Guid? categoryId,
            string? status,
            string? stockStatus,
            CancellationToken ct) =>
        {
            var result = await sender.Send(
                new GetAdminInventorySkusQuery(
                    page ?? 1,
                    pageSize ?? 20,
                    sort ?? AdminInventorySkuListSort.Default,
                    q,
                    productId,
                    categorySlug,
                    categoryId,
                    status ?? AdminInventorySkuListFilters.StatusAll,
                    stockStatus ?? AdminInventorySkuListFilters.StockAll),
                ct);

            return Results.Ok(result);
        });

        adminInv.MapPost("/skus/availability", async (
            ISender sender,
            GetSkuAvailabilityBatchRequest req,
            CancellationToken ct) =>
        {
            var result = await sender.Send(
                new GetSkuAvailabilityBatchQuery(req.SkuIds ?? []),
                ct);
            return Results.Ok(result);
        });

        adminInv.MapPost("/skus/{skuId:guid}/reserve", async (
            ISender sender,
            Guid skuId,
            ReserveStockRequest req,
            CancellationToken ct) =>
        {
            var reservationId = await sender.Send(
                new ReserveStockCommand(skuId, req.Quantity, req.ExpiresAt), ct);
            return Results.Created($"/api/admin/inventory/reservations/{reservationId}", new { reservationId });
        });

        adminInv.MapPost("/reservations/{reservationId:guid}/confirm", async (
            ISender sender,
            Guid reservationId,
            CancellationToken ct) =>
        {
            await sender.Send(new ConfirmStockReservationCommand(reservationId), ct);
            return Results.NoContent();
        });

        adminInv.MapPost("/reservations/{reservationId:guid}/cancel", async (
            ISender sender,
            Guid reservationId,
            CancellationToken ct) =>
        {
            await sender.Send(new CancelStockReservationCommand(reservationId), ct);
            return Results.NoContent();
        });
    }
}

public sealed record CreateInventoryForSkuRequest(int InitialQuantity = 0);

public sealed record StockChangeRequest(int Quantity, string? Reason);

public sealed record ReserveStockRequest(int Quantity, DateTimeOffset? ExpiresAt);

public sealed record GetSkuAvailabilityBatchRequest(IReadOnlyList<Guid>? SkuIds);
