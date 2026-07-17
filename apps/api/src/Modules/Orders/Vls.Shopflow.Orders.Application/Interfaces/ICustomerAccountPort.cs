namespace Vls.Shopflow.Orders.Application.Interfaces;

/// <summary>
/// Cross-module port for creating/signing-in customer accounts during guest order claim.
/// Implemented in the HttpApi / IdentityAccess composition root.
/// </summary>
public interface ICustomerAccountPort
{
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken);

    Task<CustomerAccountCreateResult> RegisterAsync(
        string email,
        string password,
        string fullName,
        string? phone,
        CancellationToken cancellationToken);

    Task SignInAsync(Guid customerUserId, CancellationToken cancellationToken);
}

public sealed record CustomerAccountCreateResult(
    bool Succeeded,
    Guid? CustomerUserId,
    bool IsDuplicateEmail,
    IReadOnlyList<CustomerAccountFieldError> Errors);

public sealed record CustomerAccountFieldError(string Field, string Message);
