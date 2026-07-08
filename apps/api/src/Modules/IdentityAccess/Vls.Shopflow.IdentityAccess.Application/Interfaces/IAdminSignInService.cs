namespace Vls.Shopflow.IdentityAccess.Application.Interfaces;

public interface IAdminSignInService
{
    Task<(bool Succeeded, string? ErrorMessage)> SignInAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task SignOutAsync(CancellationToken cancellationToken = default);
}
