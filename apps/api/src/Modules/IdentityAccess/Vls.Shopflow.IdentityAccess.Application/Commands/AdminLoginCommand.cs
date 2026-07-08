using MediatR;
using Vls.Shopflow.IdentityAccess.Application.DataTransferObjects;

namespace Vls.Shopflow.IdentityAccess.Application.Commands;

public sealed record AdminLoginCommand(string Email, string Password, string? IpAddress)
    : IRequest<AdminLoginResult>;

public sealed record AdminLoginResult(bool Succeeded, AdminUserDto? User, string? ErrorMessage);
