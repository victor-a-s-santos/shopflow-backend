using Vls.Shopflow.IdentityAccess.Application.Commands;

namespace Vls.Shopflow.IdentityAccess.Application.Interfaces;

public interface IAdminLoginService
{
    Task<AdminLoginResult> LoginAsync(
        string email,
        string password,
        string? ipAddress,
        CancellationToken cancellationToken = default);
}
