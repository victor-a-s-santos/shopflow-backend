namespace Vls.Shopflow.IdentityAccess.Application.DataTransferObjects;

public sealed record CustomerUserDto(
    Guid CustomerId,
    string Email,
    string FullName,
    string? Phone,
    bool EmailConfirmed,
    IReadOnlyList<string> Roles);

public sealed record RegisterCustomerResult(
    bool Succeeded,
    CustomerUserDto? Customer,
    string? ErrorMessage,
    bool IsDuplicateEmail);

public sealed record CustomerLoginResult(
    bool Succeeded,
    CustomerUserDto? Customer,
    string? ErrorMessage);

public sealed record GenericMessageResult(string Message);
