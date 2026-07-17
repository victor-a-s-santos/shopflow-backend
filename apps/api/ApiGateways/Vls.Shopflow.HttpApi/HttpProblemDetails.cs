using System.Diagnostics;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;

namespace Vls.Shopflow.HttpApi;

/// <summary>
/// Builds RFC 7807 ProblemDetails / ValidationProblemDetails with field errors and traceId.
/// </summary>
public static class HttpProblemDetails
{
    public static ValidationProblemDetails Validation(
        HttpContext ctx,
        IEnumerable<ValidationFailure> failures,
        string title = "Validation failed",
        string? detail = null)
    {
        var errors = failures
            .GroupBy(f => ToCamelCasePath(f.PropertyName))
            .ToDictionary(
                g => g.Key,
                g => g.Select(f => f.ErrorMessage).Distinct().ToArray());

        var problem = new ValidationProblemDetails(errors)
        {
            Title = title,
            Status = StatusCodes.Status400BadRequest,
            Detail = detail ?? "One or more validation errors occurred.",
            Instance = ctx.Request.Path
        };

        problem.Extensions["traceId"] = GetTraceId(ctx);
        return problem;
    }

    public static ValidationProblemDetails Validation(
        HttpContext ctx,
        ValidationException ex,
        string title = "Validation failed")
        => Validation(ctx, ex.Errors, title);

    public static ProblemDetails Conflict(
        HttpContext ctx,
        string detail,
        string? field = null,
        string? errorCode = null,
        string title = "Conflict")
    {
        var problem = new ProblemDetails
        {
            Title = title,
            Status = StatusCodes.Status409Conflict,
            Detail = detail,
            Instance = ctx.Request.Path
        };

        problem.Extensions["traceId"] = GetTraceId(ctx);
        if (!string.IsNullOrWhiteSpace(errorCode))
            problem.Extensions["errorCode"] = errorCode;

        if (!string.IsNullOrWhiteSpace(field))
        {
            problem.Extensions["errors"] = new Dictionary<string, string[]>
            {
                [ToCamelCasePath(field)] = [detail]
            };
        }

        return problem;
    }

    public static ProblemDetails Problem(
        HttpContext ctx,
        int status,
        string title,
        string detail)
    {
        var problem = new ProblemDetails
        {
            Title = title,
            Status = status,
            Detail = detail,
            Instance = ctx.Request.Path
        };
        problem.Extensions["traceId"] = GetTraceId(ctx);
        return problem;
    }

    public static ProblemDetails Unexpected(HttpContext ctx)
        => Problem(
            ctx,
            StatusCodes.Status500InternalServerError,
            "An unexpected error occurred.",
            "An unexpected error occurred. Use the traceId when contacting support.");

    public static string GetTraceId(HttpContext ctx)
        => Activity.Current?.Id ?? ctx.TraceIdentifier;

    /// <summary>
    /// Converts FluentValidation paths like "Attributes[0].CustomName" to "attributes[0].customName".
    /// </summary>
    public static string ToCamelCasePath(string? propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
            return "request";

        var parts = propertyName.Split('.');
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            var bracket = part.IndexOf('[');
            if (bracket < 0)
            {
                parts[i] = ToCamelCase(part);
                continue;
            }

            var name = part[..bracket];
            var index = part[bracket..];
            parts[i] = ToCamelCase(name) + index;
        }

        return string.Join('.', parts);
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;
        if (name.Length == 1)
            return name.ToLowerInvariant();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}
