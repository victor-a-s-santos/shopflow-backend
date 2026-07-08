using Vls.Shopflow.IdentityAccess.Application.DataTransferObjects;

namespace Vls.Shopflow.IdentityAccess.Application.Interfaces;

public interface ICurrentAdminAccessor
{
    Task<AdminUserDto?> GetCurrentAdminAsync(CancellationToken cancellationToken = default);
}
