using Microsoft.AspNetCore.Identity;
using Vls.Shopflow.IdentityAccess.Domain.Enums;

namespace Vls.Shopflow.IdentityAccess.Infrastructure.Identity;

public sealed class ShopflowUser : IdentityUser<Guid>
{
    public string? FullName { get; set; }
    public bool IsStaff { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }

    public CustomerAccessStatus AccessStatus { get; set; } = CustomerAccessStatus.PendingApproval;
    public DateTimeOffset? AccessRequestedAt { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset? AccessDecidedAt { get; set; }
    public Guid? AccessDecidedByAdminUserId { get; set; }
    public string? AccessDecisionReason { get; set; }
}

public sealed class ShopflowRole : IdentityRole<Guid>
{
}
