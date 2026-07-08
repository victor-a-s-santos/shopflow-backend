using MediatR;
using Vls.Shopflow.IdentityAccess.Application.Commands;
using Vls.Shopflow.IdentityAccess.Application.Interfaces;

namespace Vls.Shopflow.IdentityAccess.Application.CommandHandlers;

public sealed class AdminLogoutCommandHandler(IAdminSignInService adminSignInService)
    : IRequestHandler<AdminLogoutCommand>
{
    public async Task Handle(AdminLogoutCommand request, CancellationToken cancellationToken)
    {
        await adminSignInService.SignOutAsync(cancellationToken);
    }
}
