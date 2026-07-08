using MediatR;
using Vls.Shopflow.IdentityAccess.Application.DataTransferObjects;
using Vls.Shopflow.IdentityAccess.Application.Interfaces;
using Vls.Shopflow.IdentityAccess.Application.Queries;

namespace Vls.Shopflow.IdentityAccess.Application.QueryHandlers;

public sealed class GetCurrentAdminQueryHandler(ICurrentAdminAccessor currentAdminAccessor)
    : IRequestHandler<GetCurrentAdminQuery, AdminUserDto?>
{
    public Task<AdminUserDto?> Handle(GetCurrentAdminQuery request, CancellationToken cancellationToken)
        => currentAdminAccessor.GetCurrentAdminAsync(cancellationToken);
}
