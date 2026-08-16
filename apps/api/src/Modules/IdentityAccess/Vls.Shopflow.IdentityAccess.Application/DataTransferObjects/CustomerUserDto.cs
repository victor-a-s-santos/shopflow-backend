using Vls.Shopflow.IdentityAccess.Domain.Enums;

namespace Vls.Shopflow.IdentityAccess.Application.DataTransferObjects;

public sealed record CustomerUserDto(
    Guid CustomerId,
    string Email,
    string FullName,
    string? Phone,
    bool EmailConfirmed,
    IReadOnlyList<string> Roles,
    CustomerAccessStatus AccessStatus,
    DateTimeOffset? AccessRequestedAt,
    DateTimeOffset? ApprovedAt);

public sealed record RegisterCustomerFieldError(string Field, string Code, string Message);

public sealed record RegisterCustomerResult(
    bool Succeeded,
    CustomerUserDto? Customer,
    string? ErrorMessage,
    bool IsDuplicateEmail,
    IReadOnlyList<RegisterCustomerFieldError> Errors);

public sealed record CustomerLoginResult(
    bool Succeeded,
    CustomerUserDto? Customer,
    string? ErrorMessage);

public sealed record GenericMessageResult(string Message);
