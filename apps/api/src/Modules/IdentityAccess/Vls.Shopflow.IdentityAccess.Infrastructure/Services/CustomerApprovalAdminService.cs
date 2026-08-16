using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vls.Shopflow.IdentityAccess.Application.DataTransferObjects;
using Vls.Shopflow.IdentityAccess.Application.Interfaces;
using Vls.Shopflow.IdentityAccess.Application.Services;
using Vls.Shopflow.IdentityAccess.Domain.Constants;
using Vls.Shopflow.IdentityAccess.Domain.Enums;
using Vls.Shopflow.IdentityAccess.Domain.Exceptions;
using Vls.Shopflow.IdentityAccess.Infrastructure.Identity;

namespace Vls.Shopflow.IdentityAccess.Infrastructure.Services;

public sealed class CustomerApprovalAdminService(
    IdentityAccessDbContext db,
    UserManager<ShopflowUser> userManager,
    ICustomerAccessNotifier accessNotifier,
    ILogger<CustomerApprovalAdminService> logger)
    : ICustomerApprovalAdminService
{
    public async Task<PagedAdminCustomersDto> ListAsync(
        CustomerAccessStatus? status,
        string? search,
        int page,
        int pageSize,
        DateTimeOffset? createdFrom = null,
        DateTimeOffset? createdTo = null,
        string? sort = null,
        CancellationToken cancellationToken = default)
    {
        var query = CustomerUsers();

        if (status is not null)
            query = query.Where(u => u.AccessStatus == status);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(u =>
                (u.FullName != null && EF.Functions.ILike(u.FullName, $"%{term}%"))
                || (u.Email != null && EF.Functions.ILike(u.Email, $"%{term}%"))
                || (u.PhoneNumber != null && EF.Functions.ILike(u.PhoneNumber, $"%{term}%")));
        }

        if (createdFrom is not null)
            query = query.Where(u => u.CreatedAt >= createdFrom);

        if (createdTo is not null)
            query = query.Where(u => u.CreatedAt <= createdTo);

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);

        var users = await ApplySort(query, sort)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = users.Select(Map).ToList();

        return new PagedAdminCustomersDto(items, page, pageSize, totalItems, totalPages);
    }

    public Task<int> CountPendingAsync(CancellationToken cancellationToken = default)
        => CustomerUsers()
            .CountAsync(u => u.AccessStatus == CustomerAccessStatus.PendingApproval, cancellationToken);

    public async Task<AdminCustomerListItemDto?> GetByIdAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var user = await FindCustomerAsync(customerId, cancellationToken);
        return user is null ? null : Map(user);
    }

    public Task<AdminCustomerListItemDto> ApproveAsync(
        Guid customerId,
        Guid adminUserId,
        string? reason,
        CancellationToken cancellationToken = default)
        => MutateAsync(
            customerId,
            adminUserId,
            reason,
            allowedFrom:
            [
                CustomerAccessStatus.PendingApproval,
                CustomerAccessStatus.Rejected,
                CustomerAccessStatus.Suspended
            ],
            next: CustomerAccessStatus.Approved,
            cancellationToken);

    public Task<AdminCustomerListItemDto> RejectAsync(
        Guid customerId,
        Guid adminUserId,
        string? reason,
        CancellationToken cancellationToken = default)
        => MutateAsync(
            customerId,
            adminUserId,
            reason,
            allowedFrom: [CustomerAccessStatus.PendingApproval],
            next: CustomerAccessStatus.Rejected,
            cancellationToken);

    public Task<AdminCustomerListItemDto> SuspendAsync(
        Guid customerId,
        Guid adminUserId,
        string? reason,
        CancellationToken cancellationToken = default)
        => MutateAsync(
            customerId,
            adminUserId,
            reason,
            allowedFrom: [CustomerAccessStatus.Approved],
            next: CustomerAccessStatus.Suspended,
            cancellationToken);

    public Task<AdminCustomerListItemDto> ReactivateAsync(
        Guid customerId,
        Guid adminUserId,
        string? reason,
        CancellationToken cancellationToken = default)
        => MutateAsync(
            customerId,
            adminUserId,
            reason,
            allowedFrom: [CustomerAccessStatus.Suspended, CustomerAccessStatus.Rejected],
            next: CustomerAccessStatus.Approved,
            cancellationToken);

    private async Task<AdminCustomerListItemDto> MutateAsync(
        Guid customerId,
        Guid adminUserId,
        string? reason,
        CustomerAccessStatus[] allowedFrom,
        CustomerAccessStatus next,
        CancellationToken cancellationToken)
    {
        var user = await FindCustomerAsync(customerId, cancellationToken)
                   ?? throw CustomerApprovalException.NotFound();

        if (!string.IsNullOrWhiteSpace(reason)
            && reason.Trim().Length > CustomerAccessContract.MaxDecisionReasonLength)
        {
            throw CustomerApprovalException.ReasonTooLong();
        }

        if (user.AccessStatus == next)
            return Map(user);

        if (!allowedFrom.Contains(user.AccessStatus))
            throw CustomerApprovalException.InvalidTransition();

        var previous = user.AccessStatus;
        var now = DateTimeOffset.UtcNow;
        user.AccessStatus = next;
        user.AccessDecidedAt = now;
        user.AccessDecidedByAdminUserId = adminUserId;
        user.ApprovedAt = next == CustomerAccessStatus.Approved ? now : null;
        user.AccessDecisionReason = next == CustomerAccessStatus.Approved
            ? (string.IsNullOrWhiteSpace(reason) ? null : reason.Trim())
            : (string.IsNullOrWhiteSpace(reason) ? null : reason.Trim());

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to update customer access: {errors}");
        }

        if (previous != next)
            await NotifyAccessChangedAsync(user, next, cancellationToken);

        return Map(user);
    }

    private async Task NotifyAccessChangedAsync(
        ShopflowUser user,
        CustomerAccessStatus next,
        CancellationToken cancellationToken)
    {
        var notification = new CustomerAccessChangedNotification(
            user.Id,
            user.Email ?? string.Empty,
            user.FullName ?? string.Empty,
            user.AccessDecidedAt);

        try
        {
            if (next == CustomerAccessStatus.Approved)
                await accessNotifier.NotifyApprovedAsync(notification, cancellationToken);
            else if (next == CustomerAccessStatus.Rejected)
                await accessNotifier.NotifyRejectedAsync(notification, cancellationToken);
            else if (next == CustomerAccessStatus.Suspended)
                await accessNotifier.NotifySuspendedAsync(notification, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to enqueue customer access e-mail CustomerUserId={CustomerUserId} Next={Next}",
                user.Id,
                next);
        }
    }

    private IQueryable<ShopflowUser> CustomerUsers()
        => db.Users.AsNoTracking().Where(u => !u.IsStaff);

    private static IQueryable<ShopflowUser> ApplySort(IQueryable<ShopflowUser> query, string? sort)
    {
        var key = sort?.Trim().ToLowerInvariant();
        return key switch
        {
            "createdat" => query.OrderBy(u => u.CreatedAt),
            "-createdat" => query.OrderByDescending(u => u.CreatedAt),
            "email" => query.OrderBy(u => u.Email),
            "-email" => query.OrderByDescending(u => u.Email),
            "name" => query.OrderBy(u => u.FullName),
            "-name" => query.OrderByDescending(u => u.FullName),
            "requestedat" => query.OrderBy(u => u.AccessRequestedAt ?? u.CreatedAt),
            "-requestedat" => query.OrderByDescending(u => u.AccessRequestedAt ?? u.CreatedAt),
            _ => query
                .OrderBy(u => u.AccessStatus)
                .ThenByDescending(u => u.AccessRequestedAt ?? u.CreatedAt)
        };
    }

    private async Task<ShopflowUser?> FindCustomerAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(customerId.ToString());
        if (user is null || user.IsStaff)
            return null;

        if (!await userManager.IsInRoleAsync(user, AuthRoles.Customer))
            return null;

        return user;
    }

    private static AdminCustomerListItemDto Map(ShopflowUser user)
        => new(
            user.Id,
            user.Email ?? string.Empty,
            user.FullName ?? string.Empty,
            user.PhoneNumber,
            user.EmailConfirmed,
            user.AccessStatus,
            user.CreatedAt,
            user.AccessRequestedAt,
            user.ApprovedAt,
            user.AccessDecidedAt,
            user.AccessDecidedByAdminUserId,
            user.AccessDecisionReason);
}
