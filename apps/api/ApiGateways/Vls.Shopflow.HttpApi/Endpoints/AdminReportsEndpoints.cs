using MediatR;
using Vls.Shopflow.IdentityAccess.Domain.Constants;
using Vls.Shopflow.Orders.Application.Queries;
using Vls.Shopflow.Orders.Application.Validators;

namespace Vls.Shopflow.HttpApi.Endpoints;

public static class AdminReportsEndpoints
{
    public static RouteGroupBuilder MapAdminReportsEndpoints(this RouteGroupBuilder group)
    {
        var reports = group.MapGroup("/admin/reports")
            .WithTags("AdminReports")
            .RequireAuthorization(AuthPolicies.Backoffice);

        reports.MapGet("/product-demand", async (
            ISender sender,
            int? page,
            int? pageSize,
            DateTimeOffset? from,
            DateTimeOffset? to,
            string? q,
            string? sort,
            CancellationToken ct) =>
        {
            var result = await sender.Send(
                new GetAdminProductDemandQuery(
                    page ?? 1,
                    pageSize ?? AdminDemandReportLimits.DefaultPageSize,
                    from,
                    to,
                    q,
                    sort),
                ct);

            return Results.Ok(result);
        });

        reports.MapGet("/product-demand/{skuId:guid}/orders", async (
            ISender sender,
            Guid skuId,
            int? page,
            int? pageSize,
            DateTimeOffset? from,
            DateTimeOffset? to,
            string? sort,
            CancellationToken ct) =>
        {
            var result = await sender.Send(
                new GetAdminProductDemandOrdersQuery(
                    skuId,
                    page ?? 1,
                    pageSize ?? AdminDemandReportLimits.DefaultPageSize,
                    from,
                    to,
                    sort),
                ct);

            return Results.Ok(result);
        });

        reports.MapGet("/unpaid-orders", async (
            ISender sender,
            int? page,
            int? pageSize,
            DateTimeOffset? from,
            DateTimeOffset? to,
            string? q,
            string? sort,
            CancellationToken ct) =>
        {
            var result = await sender.Send(
                new GetAdminUnpaidOrdersQuery(
                    page ?? 1,
                    pageSize ?? AdminDemandReportLimits.DefaultPageSize,
                    from,
                    to,
                    q,
                    sort),
                ct);

            return Results.Ok(result);
        });

        return group;
    }
}
