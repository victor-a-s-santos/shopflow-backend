using MediatR;
using Vls.Shopflow.IdentityAccess.Application.Commands;
using Vls.Shopflow.IdentityAccess.Application.DataTransferObjects;
using Vls.Shopflow.IdentityAccess.Application.Interfaces;
using Vls.Shopflow.IdentityAccess.Application.Queries;

namespace Vls.Shopflow.IdentityAccess.Application.CommandHandlers;

public sealed class RegisterCustomerCommandHandler(ICustomerRegistrationService service)
    : IRequestHandler<RegisterCustomerCommand, RegisterCustomerResult>
{
    public Task<RegisterCustomerResult> Handle(RegisterCustomerCommand request, CancellationToken cancellationToken)
        => service.RegisterAsync(request.Email, request.Password, request.FullName, request.Phone, cancellationToken);
}

public sealed class LoginCustomerCommandHandler(ICustomerLoginService service)
    : IRequestHandler<LoginCustomerCommand, CustomerLoginResult>
{
    public Task<CustomerLoginResult> Handle(LoginCustomerCommand request, CancellationToken cancellationToken)
        => service.LoginAsync(request.Email, request.Password, request.IpAddress, cancellationToken);
}

public sealed class CustomerLogoutCommandHandler(ICustomerSignInService signInService)
    : IRequestHandler<CustomerLogoutCommand>
{
    public Task Handle(CustomerLogoutCommand request, CancellationToken cancellationToken)
    {
        return signInService.SignOutAsync(cancellationToken);
    }
}

public sealed class ForgotCustomerPasswordCommandHandler(ICustomerPasswordService service)
    : IRequestHandler<ForgotCustomerPasswordCommand, GenericMessageResult>
{
    public Task<GenericMessageResult> Handle(ForgotCustomerPasswordCommand request, CancellationToken cancellationToken)
        => service.ForgotPasswordAsync(request.Email, cancellationToken);
}

public sealed class ResetCustomerPasswordCommandHandler(ICustomerPasswordService service)
    : IRequestHandler<ResetCustomerPasswordCommand, ResetCustomerPasswordResult>
{
    public Task<ResetCustomerPasswordResult> Handle(
        ResetCustomerPasswordCommand request,
        CancellationToken cancellationToken)
        => service.ResetPasswordAsync(request.Email, request.Token, request.NewPassword, cancellationToken);
}

public sealed class ConfirmCustomerEmailCommandHandler(ICustomerPasswordService service)
    : IRequestHandler<ConfirmCustomerEmailCommand, (bool Succeeded, string? ErrorMessage)>
{
    public Task<(bool Succeeded, string? ErrorMessage)> Handle(
        ConfirmCustomerEmailCommand request,
        CancellationToken cancellationToken)
        => service.ConfirmEmailAsync(request.Email, request.Token, cancellationToken);
}

public sealed class GetCurrentCustomerQueryHandler(ICurrentCustomerAccessor accessor)
    : IRequestHandler<GetCurrentCustomerQuery, CustomerUserDto?>
{
    public Task<CustomerUserDto?> Handle(GetCurrentCustomerQuery request, CancellationToken cancellationToken)
        => accessor.GetCurrentCustomerAsync(cancellationToken);
}
