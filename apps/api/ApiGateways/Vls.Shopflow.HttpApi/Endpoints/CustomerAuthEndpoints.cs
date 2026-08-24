using MediatR;
using Vls.Shopflow.IdentityAccess.Application.Commands;
using Vls.Shopflow.IdentityAccess.Application.Queries;
using Vls.Shopflow.IdentityAccess.Application.Security;
using Vls.Shopflow.IdentityAccess.Domain.Constants;
using Vls.Shopflow.IdentityAccess.Infrastructure;

namespace Vls.Shopflow.HttpApi.Endpoints;

public static class CustomerAuthEndpoints
{
    private const string ForgotPasswordMessage =
        "If the email is registered, we will send password reset instructions.";

    public static RouteGroupBuilder MapCustomerAuthEndpoints(this RouteGroupBuilder group)
    {
        var customer = group.MapGroup("/auth/customer").WithTags("CustomerAuth");

        customer.MapPost("/register", async (
            ISender sender,
            RegisterCustomerRequest req,
            CancellationToken ct) =>
        {
            var result = await sender.Send(
                new RegisterCustomerCommand(req.Email, req.Password, req.FullName, req.Phone), ct);

            if (!result.Succeeded || result.Customer is null)
            {
                if (result.IsDuplicateEmail)
                {
                    return Results.Json(
                        new
                        {
                            code = "ACCOUNT_ALREADY_EXISTS",
                            message = result.ErrorMessage ?? "Já existe uma conta com este e-mail."
                        },
                        statusCode: StatusCodes.Status409Conflict);
                }

                var errors = result.Errors
                    .GroupBy(e => e.Field)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(e => e.Message).Distinct().ToArray());

                return Results.Json(
                    new
                    {
                        code = CustomerPasswordPolicy.TooWeakCode,
                        message = result.ErrorMessage ?? CustomerPasswordPolicy.SummaryMessage,
                        errors
                    },
                    statusCode: StatusCodes.Status400BadRequest);
            }

            return Results.Created("/api/auth/customer/me", CustomerResponseMapper.ToRegisterResponse(result.Customer, result.Message));
        })
        .RequireRateLimiting(DependencyInjection.CustomerRegisterRateLimitPolicy)
        .DisableAntiforgery();

        customer.MapPost("/login", async (
            ISender sender,
            HttpContext ctx,
            CustomerLoginRequest req,
            CancellationToken ct) =>
        {
            var ip = ctx.Connection.RemoteIpAddress?.ToString();
            var result = await sender.Send(new LoginCustomerCommand(req.Email, req.Password, ip), ct);

            if (!result.Succeeded || result.Customer is null)
                return Results.Json(
                    new { message = result.ErrorMessage ?? "Invalid email or password." },
                    statusCode: StatusCodes.Status401Unauthorized);

            return Results.Ok(CustomerResponseMapper.ToAuthResponse(result.Customer));
        })
        .RequireRateLimiting(DependencyInjection.CustomerLoginRateLimitPolicy)
        .DisableAntiforgery();

        customer.MapPost("/logout", async (ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new CustomerLogoutCommand(), ct);
            return Results.NoContent();
        })
        .RequireAuthorization(AuthPolicies.Customer);

        customer.MapGet("/me", async (ISender sender, CancellationToken ct) =>
        {
            var user = await sender.Send(new GetCurrentCustomerQuery(), ct);
            if (user is null)
                return Results.Unauthorized();

            return Results.Ok(CustomerResponseMapper.ToAuthResponse(user));
        })
        .RequireAuthorization(AuthPolicies.Customer);

        customer.MapPost("/forgot-password", async (
            ISender sender,
            CustomerForgotPasswordRequest req,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new ForgotCustomerPasswordCommand(req.Email), ct);
            return Results.Ok(new { message = result.Message });
        })
        .RequireRateLimiting(DependencyInjection.CustomerForgotPasswordRateLimitPolicy)
        .DisableAntiforgery();

        customer.MapPost("/reset-password", async (
            ISender sender,
            ResetCustomerPasswordRequest req,
            CancellationToken ct) =>
        {
            var result = await sender.Send(
                new ResetCustomerPasswordCommand(req.Email, req.Token, req.NewPassword), ct);

            if (!result.Succeeded)
            {
                if (result.Errors is { Count: > 0 })
                {
                    var errors = result.Errors
                        .GroupBy(e => e.Field)
                        .ToDictionary(
                            g => g.Key,
                            g => g.Select(e => e.Message).Distinct().ToArray());

                    return Results.Json(
                        new
                        {
                            code = result.Code ?? CustomerPasswordPolicy.TooWeakCode,
                            message = result.ErrorMessage ?? CustomerPasswordPolicy.SummaryMessage,
                            errors
                        },
                        statusCode: StatusCodes.Status400BadRequest);
                }

                return Results.Json(
                    new { message = result.ErrorMessage ?? "Unable to complete the request." },
                    statusCode: StatusCodes.Status400BadRequest);
            }

            return Results.NoContent();
        })
        .RequireRateLimiting(DependencyInjection.CustomerResetPasswordRateLimitPolicy)
        .DisableAntiforgery();

        customer.MapPost("/confirm-email", async (
            ISender sender,
            ConfirmCustomerEmailRequest req,
            CancellationToken ct) =>
        {
            var result = await sender.Send(
                new ConfirmCustomerEmailCommand(req.Email, req.Token), ct);

            if (!result.Succeeded)
                return Results.Json(
                    new { message = result.ErrorMessage ?? "Unable to complete the request." },
                    statusCode: StatusCodes.Status400BadRequest);

            return Results.NoContent();
        })
        .DisableAntiforgery();

        return group;
    }
}

public sealed record RegisterCustomerRequest(
    string Email,
    string Password,
    string FullName,
    string? Phone);

public sealed record CustomerLoginRequest(string Email, string Password);

public sealed record CustomerForgotPasswordRequest(string Email);

public sealed record ResetCustomerPasswordRequest(string Email, string Token, string NewPassword);

public sealed record ConfirmCustomerEmailRequest(string Email, string Token);
