using MediatR;
using Vls.Shopflow.IdentityAccess.Application.DataTransferObjects;

namespace Vls.Shopflow.IdentityAccess.Application.Queries;

public sealed record GetCurrentAdminQuery : IRequest<AdminUserDto?>;
