using MediatR;
using Vls.Shopflow.IdentityAccess.Application.DataTransferObjects;

namespace Vls.Shopflow.IdentityAccess.Application.Commands;

public sealed record RegisterCustomerCommand(
    string Email,
    string Password,
    string FullName,
    string? Phone) : IRequest<RegisterCustomerResult>;

public sealed record LoginCustomerCommand(
    string Email,
    string Password,
    string? IpAddress) : IRequest<CustomerLoginResult>;

public sealed record CustomerLogoutCommand : IRequest;

public sealed record ForgotCustomerPasswordCommand(string Email) : IRequest<GenericMessageResult>;

public sealed record ResetCustomerPasswordCommand(
    string Email,
    string Token,
    string NewPassword) : IRequest<ResetCustomerPasswordResult>;

public sealed record ConfirmCustomerEmailCommand(
    string Email,
    string Token) : IRequest<(bool Succeeded, string? ErrorMessage)>;
