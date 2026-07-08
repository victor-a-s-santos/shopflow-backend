using MediatR;
using Microsoft.AspNetCore.Antiforgery;
using Vls.Shopflow.IdentityAccess.Application.Commands;
using Vls.Shopflow.IdentityAccess.Application.Queries;
using Vls.Shopflow.IdentityAccess.Domain.Constants;
using Vls.Shopflow.IdentityAccess.Infrastructure;

namespace Vls.Shopflow.HttpApi.Endpoints;

public static class AdminAuthEndpoints
{
    public static RouteGroupBuilder MapAdminAuthEndpoints(this RouteGroupBuilder group)
    {
        var auth = group.MapGroup("/auth").WithTags("Auth");

        auth.MapGet("/csrf", (IAntiforgery antiforgery, HttpContext ctx) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(ctx);
            return Results.Ok(new { token = tokens.RequestToken });
        });

        var admin = auth.MapGroup("/admin").WithTags("AdminAuth");

        admin.MapPost("/login", async (
            ISender sender,
            HttpContext ctx,
            AdminLoginRequest req,
            CancellationToken ct) =>
        {
            var ip = ctx.Connection.RemoteIpAddress?.ToString();
            var result = await sender.Send(new AdminLoginCommand(req.Email, req.Password, ip), ct);

            if (!result.Succeeded || result.User is null)
                return Results.Json(new { message = result.ErrorMessage ?? "Invalid email or password." },
                    statusCode: StatusCodes.Status401Unauthorized);

            return Results.Ok(new
            {
                id = result.User.Id,
                name = result.User.Name,
                email = result.User.Email,
                roles = result.User.Roles
            });
        })
        .RequireRateLimiting(DependencyInjection.AdminLoginRateLimitPolicy)
        .DisableAntiforgery();

        admin.MapPost("/logout", async (ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new AdminLogoutCommand(), ct);
            return Results.NoContent();
        })
        .RequireAuthorization(AuthPolicies.Backoffice);

        admin.MapGet("/me", async (ISender sender, CancellationToken ct) =>
        {
            var user = await sender.Send(new GetCurrentAdminQuery(), ct);
            if (user is null)
                return Results.Unauthorized();

            return Results.Ok(new
            {
                id = user.Id,
                name = user.Name,
                email = user.Email,
                roles = user.Roles
            });
        })
        .RequireAuthorization(AuthPolicies.Backoffice);

        return group;
    }
}

public sealed record AdminLoginRequest(string Email, string Password);
