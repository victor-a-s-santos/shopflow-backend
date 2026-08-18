using Vls.Shopflow.IdentityAccess.Application.DataTransferObjects;
using Vls.Shopflow.IdentityAccess.Application.Interfaces;
using Vls.Shopflow.IdentityAccess.Application.Services;
using Vls.Shopflow.IdentityAccess.Domain.Constants;
using Vls.Shopflow.IdentityAccess.Domain.Enums;

namespace Vls.Shopflow.HttpApi.Endpoints;

internal static class CustomerResponseMapper
{
    public static object ToAuthResponse(CustomerUserDto user)
        => new
        {
            customerId = user.CustomerId,
            email = user.Email,
            fullName = user.FullName,
            name = user.FullName,
            phone = user.Phone,
            emailConfirmed = user.EmailConfirmed,
            roles = user.Roles,
            approvalStatus = CustomerAccessContract.ToPublicApprovalStatus(user.AccessStatus),
            accessStatus = user.AccessStatus.ToString(),
            approvalRequestedAt = user.AccessRequestedAt,
            accessRequestedAt = user.AccessRequestedAt,
            approvedAt = user.ApprovedAt
        };

    public static object ToRegisterResponse(CustomerUserDto user, string? message)
        => new
        {
            customerId = user.CustomerId,
            email = user.Email,
            fullName = user.FullName,
            name = user.FullName,
            phone = user.Phone,
            emailConfirmed = user.EmailConfirmed,
            approvalStatus = CustomerAccessContract.ToPublicApprovalStatus(user.AccessStatus),
            accessStatus = user.AccessStatus.ToString(),
            approvalRequestedAt = user.AccessRequestedAt,
            accessRequestedAt = user.AccessRequestedAt,
            approvedAt = user.ApprovedAt,
            message = message
                ?? (user.AccessStatus == CustomerAccessStatus.PendingApproval
                    ? CustomerAccessContract.RegisterPendingMessage
                    : CustomerAccessContract.RegisterApprovedMessage)
        };

    public static object ToAdminCustomerResponse(AdminCustomerListItemDto user)
    {
        var approvalStatus = CustomerAccessContract.ToPublicApprovalStatus(user.AccessStatus);
        return new
        {
            customerId = user.CustomerId,
            email = user.Email,
            name = user.FullName,
            fullName = user.FullName,
            phone = user.Phone,
            emailConfirmed = user.EmailConfirmed,
            approvalStatus,
            accessStatus = user.AccessStatus.ToString(),
            createdAt = user.CreatedAt,
            approvalRequestedAt = user.AccessRequestedAt,
            accessRequestedAt = user.AccessRequestedAt,
            approvedAt = user.ApprovedAt,
            rejectedAt = user.AccessStatus == CustomerAccessStatus.Rejected ? user.AccessDecidedAt : null,
            suspendedAt = user.AccessStatus == CustomerAccessStatus.Suspended ? user.AccessDecidedAt : null,
            approvedByAdminId = user.AccessStatus == CustomerAccessStatus.Approved ? user.AccessDecidedByAdminUserId : null,
            rejectedByAdminId = user.AccessStatus == CustomerAccessStatus.Rejected ? user.AccessDecidedByAdminUserId : null,
            suspendedByAdminId = user.AccessStatus == CustomerAccessStatus.Suspended ? user.AccessDecidedByAdminUserId : null,
            rejectionReason = user.AccessStatus == CustomerAccessStatus.Rejected ? user.AccessDecisionReason : null,
            suspensionReason = user.AccessStatus == CustomerAccessStatus.Suspended ? user.AccessDecisionReason : null,
            accessDecidedAt = user.AccessDecidedAt,
            accessDecidedByAdminUserId = user.AccessDecidedByAdminUserId,
            accessDecisionReason = user.AccessDecisionReason
        };
    }
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

    public static IResult CustomerNotFound(HttpContext ctx)
    {
        var problem = HttpProblemDetails.Problem(
            ctx,
            StatusCodes.Status404NotFound,
            "Not found",
            StoreAccessMessages.CustomerNotFound);
        problem.Extensions["code"] = StoreAccessErrorCodes.CustomerNotFound;
        problem.Extensions["message"] = StoreAccessMessages.CustomerNotFound;
        return Results.Json(problem, statusCode: StatusCodes.Status404NotFound);
    }

    public static IResult InvalidApprovalStatusFilter(HttpContext ctx)
    {
        var problem = HttpProblemDetails.Problem(
            ctx,
            StatusCodes.Status400BadRequest,
            "Bad Request",
            StoreAccessMessages.InvalidStatusFilter);
        problem.Extensions["code"] = StoreAccessErrorCodes.CustomerApprovalInvalidStatus;
        problem.Extensions["message"] = StoreAccessMessages.InvalidStatusFilter;
        return Results.Json(problem, statusCode: StatusCodes.Status400BadRequest);
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
