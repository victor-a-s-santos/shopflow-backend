using MediatR;
using Vls.Shopflow.IdentityAccess.Application.Commands;
using Vls.Shopflow.IdentityAccess.Application.Interfaces;

namespace Vls.Shopflow.IdentityAccess.Application.CommandHandlers;

public sealed class AdminLoginCommandHandler(IAdminLoginService adminLoginService)
    : IRequestHandler<AdminLoginCommand, AdminLoginResult>
{
    public Task<AdminLoginResult> Handle(AdminLoginCommand request, CancellationToken cancellationToken)
        => adminLoginService.LoginAsync(request.Email, request.Password, request.IpAddress, cancellationToken);
}
