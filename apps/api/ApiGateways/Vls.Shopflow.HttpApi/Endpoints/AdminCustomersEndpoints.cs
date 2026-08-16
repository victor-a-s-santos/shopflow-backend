using MediatR;
using Vls.Shopflow.IdentityAccess.Application.Commands;
using Vls.Shopflow.IdentityAccess.Application.Interfaces;
using Vls.Shopflow.IdentityAccess.Application.Queries;
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

        admin.MapGet("", async (
            ISender sender,
            string? status,
            string? q,
            int? page,
            int? pageSize,
            CancellationToken ct) =>
        {
            CustomerAccessStatus? parsed = null;
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (!Enum.TryParse<CustomerAccessStatus>(status, ignoreCase: true, out var value)
                    || !Enum.IsDefined(value))
                {
                    return Results.Json(
                        new { message = "Invalid access status filter." },
                        statusCode: StatusCodes.Status400BadRequest);
                }

                parsed = value;
            }

            var result = await sender.Send(
                new GetAdminCustomersQuery(parsed, q, page ?? 1, pageSize ?? 20),
                ct);

            return Results.Ok(new
            {
                items = result.Items.Select(CustomerResponseMapper.ToAdminCustomerResponse),
                page = result.Page,
                pageSize = result.PageSize,
                totalItems = result.TotalItems,
                totalPages = result.TotalPages
            });
        });

        admin.MapGet("/pending-count", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetPendingCustomerCountQuery(), ct);
            return Results.Ok(new { pendingCount = result.PendingCount });
        });

        admin.MapGet("/{customerId:guid}", async (
            ISender sender,
            Guid customerId,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetAdminCustomerByIdQuery(customerId), ct);
            return result is null
                ? Results.NotFound()
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
}

public sealed record CustomerAccessDecisionRequest(string? Reason);
