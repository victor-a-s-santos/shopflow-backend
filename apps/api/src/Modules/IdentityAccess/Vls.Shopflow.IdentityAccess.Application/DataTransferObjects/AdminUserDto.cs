namespace Vls.Shopflow.IdentityAccess.Application.DataTransferObjects;

public sealed record AdminUserDto(
    Guid Id,
    string Name,
    string Email,
    IReadOnlyList<string> Roles);
