using MediatR;
using Vls.Shopflow.IdentityAccess.Application.Commands;
using Vls.Shopflow.IdentityAccess.Application.Interfaces;
using Vls.Shopflow.IdentityAccess.Application.Queries;
using Vls.Shopflow.IdentityAccess.Application.Services;
using Vls.Shopflow.IdentityAccess.Domain.Constants;
using Vls.Shopflow.IdentityAccess.Domain.Enums;

namespace Vls.Shopflow.HttpApi.Endpoints;

public static class AdminCustomersEndpoints
{
    public static RouteGroupBuilder MapAdminCustomersEndpoints(this RouteGroupBuilder group)
    {
        var admin = group.MapGroup("/admin/customers")
            .WithTags("AdminCustomers")
            .RequireAuthorization(AuthPolicies.Backoffice);

        admin.MapGet("", ListCustomers);
        admin.MapGet("/approvals", ListApprovals);
        admin.MapGet("/approvals/count", GetPendingCount);
        admin.MapGet("/pending-count", GetPendingCount);

        admin.MapGet("/{customerId:guid}", async (
            HttpContext ctx,
            ISender sender,
            Guid customerId,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetAdminCustomerByIdQuery(customerId), ct);
            return result is null
                ? StoreAccessHttp.CustomerNotFound(ctx)
                : Results.Ok(CustomerResponseMapper.ToAdminCustomerResponse(result));
        });

        admin.MapPost("/{customerId:guid}/approve", async (
            ISender sender,
            ICurrentAdminAccessor currentAdmin,
            Guid customerId,
            CustomerAccessDecisionRequest? req,
            CancellationToken ct) =>
        {
            var adminUser = await currentAdmin.GetCurrentAdminAsync(ct);
            if (adminUser is null)
                return Results.Unauthorized();

            var result = await sender.Send(
                new ApproveCustomerCommand(customerId, adminUser.Id, req?.Reason),
                ct);
            return Results.Ok(CustomerResponseMapper.ToAdminCustomerResponse(result));
        });

        admin.MapPost("/{customerId:guid}/reject", async (
            ISender sender,
            ICurrentAdminAccessor currentAdmin,
            Guid customerId,
            CustomerAccessDecisionRequest? req,
            CancellationToken ct) =>
        {
            var adminUser = await currentAdmin.GetCurrentAdminAsync(ct);
            if (adminUser is null)
                return Results.Unauthorized();

            var result = await sender.Send(
                new RejectCustomerCommand(customerId, adminUser.Id, req?.Reason),
                ct);
            return Results.Ok(CustomerResponseMapper.ToAdminCustomerResponse(result));
        });

        admin.MapPost("/{customerId:guid}/suspend", async (
            ISender sender,
            ICurrentAdminAccessor currentAdmin,
            Guid customerId,
            CustomerAccessDecisionRequest? req,
            CancellationToken ct) =>
        {
            var adminUser = await currentAdmin.GetCurrentAdminAsync(ct);
            if (adminUser is null)
                return Results.Unauthorized();

            var result = await sender.Send(
                new SuspendCustomerCommand(customerId, adminUser.Id, req?.Reason),
                ct);
            return Results.Ok(CustomerResponseMapper.ToAdminCustomerResponse(result));
        });

        admin.MapPost("/{customerId:guid}/reactivate", async (
            ISender sender,
            ICurrentAdminAccessor currentAdmin,
            Guid customerId,
            CustomerAccessDecisionRequest? req,
            CancellationToken ct) =>
        {
            var adminUser = await currentAdmin.GetCurrentAdminAsync(ct);
            if (adminUser is null)
                return Results.Unauthorized();

            var result = await sender.Send(
                new ReactivateCustomerCommand(customerId, adminUser.Id, req?.Reason),
                ct);
            return Results.Ok(CustomerResponseMapper.ToAdminCustomerResponse(result));
        });

        return group;
    }

    private static Task<IResult> ListCustomers(
        HttpContext ctx,
        ISender sender,
        string? status,
        string? q,
        int? page,
        int? pageSize,
        DateTimeOffset? createdFrom,
        DateTimeOffset? createdTo,
        string? sort,
        CancellationToken ct)
        => ListAsync(ctx, sender, status, q, page, pageSize, createdFrom, createdTo, sort, defaultPending: false, ct);

    private static Task<IResult> ListApprovals(
        HttpContext ctx,
        ISender sender,
        string? status,
        string? q,
        int? page,
        int? pageSize,
        DateTimeOffset? createdFrom,
        DateTimeOffset? createdTo,
        string? sort,
        CancellationToken ct)
        => ListAsync(ctx, sender, status, q, page, pageSize, createdFrom, createdTo, sort, defaultPending: true, ct);

    private static async Task<IResult> GetPendingCount(ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new GetPendingCustomerCountQuery(), ct);
        return Results.Ok(new { pending = result.PendingCount, pendingCount = result.PendingCount });
    }

    private static async Task<IResult> ListAsync(
        HttpContext ctx,
        ISender sender,
        string? status,
        string? q,
        int? page,
        int? pageSize,
        DateTimeOffset? createdFrom,
        DateTimeOffset? createdTo,
        string? sort,
        bool defaultPending,
        CancellationToken ct)
    {
        CustomerAccessStatus? parsed = null;
        if (string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            parsed = null;
        }
        else if (!string.IsNullOrWhiteSpace(status))
        {
            if (!StoreAccessPolicy.TryParseAccessStatus(status, out var value))
                return StoreAccessHttp.InvalidApprovalStatusFilter(ctx);

            parsed = value;
        }
        else if (defaultPending)
        {
            parsed = CustomerAccessStatus.PendingApproval;
        }

        var result = await sender.Send(
            new GetAdminCustomersQuery(
                parsed,
                q,
                page ?? 1,
                pageSize ?? 20,
                createdFrom,
                createdTo,
                sort),
            ct);

        return Results.Ok(new
        {
            items = result.Items.Select(CustomerResponseMapper.ToAdminCustomerResponse),
            page = result.Page,
            pageSize = result.PageSize,
            totalItems = result.TotalItems,
            totalPages = result.TotalPages
        });
    }
}

public sealed record CustomerAccessDecisionRequest(string? Reason);
