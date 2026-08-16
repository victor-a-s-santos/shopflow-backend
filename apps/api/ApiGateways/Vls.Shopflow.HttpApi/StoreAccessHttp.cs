using Vls.Shopflow.IdentityAccess.Application.DataTransferObjects;
using Vls.Shopflow.IdentityAccess.Application.Interfaces;

namespace Vls.Shopflow.HttpApi.Endpoints;

internal static class CustomerResponseMapper
{
    public static object ToAuthResponse(CustomerUserDto user)
        => new
        {
            customerId = user.CustomerId,
            email = user.Email,
            fullName = user.FullName,
            phone = user.Phone,
            emailConfirmed = user.EmailConfirmed,
            roles = user.Roles,
            accessStatus = user.AccessStatus.ToString(),
            accessRequestedAt = user.AccessRequestedAt,
            approvedAt = user.ApprovedAt
        };

    public static object ToRegisterResponse(CustomerUserDto user)
        => new
        {
            customerId = user.CustomerId,
            email = user.Email,
            fullName = user.FullName,
            phone = user.Phone,
            emailConfirmed = user.EmailConfirmed,
            accessStatus = user.AccessStatus.ToString(),
            accessRequestedAt = user.AccessRequestedAt,
            approvedAt = user.ApprovedAt
        };

    public static object ToAdminCustomerResponse(AdminCustomerListItemDto user)
        => new
        {
            customerId = user.CustomerId,
            email = user.Email,
            fullName = user.FullName,
            phone = user.Phone,
            emailConfirmed = user.EmailConfirmed,
            accessStatus = user.AccessStatus.ToString(),
            createdAt = user.CreatedAt,
            accessRequestedAt = user.AccessRequestedAt,
            approvedAt = user.ApprovedAt,
            accessDecidedAt = user.AccessDecidedAt,
            accessDecidedByAdminUserId = user.AccessDecidedByAdminUserId,
            accessDecisionReason = user.AccessDecisionReason
        };
}

internal static class StoreAccessHttp
{
    public static IResult Denied(HttpContext ctx, StoreAccessDecision decision)
    {
        var problem = HttpProblemDetails.Problem(
            ctx,
            decision.StatusCode,
            decision.StatusCode == StatusCodes.Status401Unauthorized ? "Unauthorized" : "Forbidden",
            decision.Message ?? "Access denied.");
        if (!string.IsNullOrWhiteSpace(decision.Code))
            problem.Extensions["code"] = decision.Code;
        problem.Extensions["message"] = decision.Message;
        return Results.Json(problem, statusCode: decision.StatusCode);
    }

    public static async Task<IResult?> EnsureCheckoutAllowedAsync(
        HttpContext ctx,
        IStoreAccessPolicy policy,
        ICurrentCustomerAccessor currentCustomer,
        CancellationToken cancellationToken)
    {
        var customer = await currentCustomer.GetCurrentCustomerAsync(cancellationToken);
        var decision = policy.EvaluateCheckout(customer);
        return decision.Allowed ? null : Denied(ctx, decision);
    }

    public static async Task<(CustomerUserDto? Customer, IResult? Denied)> ResolveCheckoutCustomerAsync(
        HttpContext ctx,
        IStoreAccessPolicy policy,
        ICurrentCustomerAccessor currentCustomer,
        CancellationToken cancellationToken)
    {
        var customer = await currentCustomer.GetCurrentCustomerAsync(cancellationToken);
        var decision = policy.EvaluateCheckout(customer);
        return decision.Allowed ? (customer, null) : (customer, Denied(ctx, decision));
    }
}
