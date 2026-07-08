using Microsoft.AspNetCore.Identity;

namespace Vls.Shopflow.IdentityAccess.Infrastructure.Identity;

public sealed class ShopflowUser : IdentityUser<Guid>
{
    public string? FullName { get; set; }
    public bool IsStaff { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
}

public sealed class ShopflowRole : IdentityRole<Guid>
{
}
