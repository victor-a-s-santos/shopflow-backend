using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.IdentityAccess.Application.DataTransferObjects;
using Vls.Shopflow.IdentityAccess.Application.Interfaces;
using Vls.Shopflow.IdentityAccess.Domain.Constants;
using Vls.Shopflow.IdentityAccess.Domain.Enums;
using Vls.Shopflow.IdentityAccess.Domain.Exceptions;
using Vls.Shopflow.IdentityAccess.Infrastructure.Identity;

namespace Vls.Shopflow.IdentityAccess.Infrastructure.Services;

public sealed class CustomerApprovalAdminService(
    IdentityAccessDbContext db,
    UserManager<ShopflowUser> userManager)
    : ICustomerApprovalAdminService
{
    public async Task<PagedAdminCustomersDto> ListAsync(
        CustomerAccessStatus? status,
        string? search,
        int page,
        int pageSize,
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

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);

        var users = await query
            .OrderBy(u => u.AccessStatus)
            .ThenByDescending(u => u.AccessRequestedAt ?? u.CreatedAt)
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
            allowedFrom: [CustomerAccessStatus.PendingApproval, CustomerAccessStatus.Rejected],
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
                   ?? throw new KeyNotFoundException("Customer not found.");

        if (user.AccessStatus == next)
            return Map(user);

        if (!allowedFrom.Contains(user.AccessStatus))
        {
            throw CustomerApprovalException.InvalidTransition(
                $"Cannot change customer access from {user.AccessStatus} to {next}.");
        }

        var now = DateTimeOffset.UtcNow;
        user.AccessStatus = next;
        user.AccessDecidedAt = now;
        user.AccessDecidedByAdminUserId = adminUserId;
        user.AccessDecisionReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        user.ApprovedAt = next == CustomerAccessStatus.Approved ? now : null;

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to update customer access: {errors}");
        }

        return Map(user);
    }

    private IQueryable<ShopflowUser> CustomerUsers()
        => db.Users.AsNoTracking().Where(u => !u.IsStaff);

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
