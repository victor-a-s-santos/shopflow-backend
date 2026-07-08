using Vls.Shopflow.IdentityAccess.Application.DataTransferObjects;

namespace Vls.Shopflow.IdentityAccess.Application.Interfaces;

public interface IIdentityEmailSender
{
    Task SendEmailConfirmationAsync(string email, string token, CancellationToken cancellationToken = default);

    Task SendPasswordResetAsync(string email, string token, CancellationToken cancellationToken = default);
}

public interface ICustomerRegistrationService
{
    Task<RegisterCustomerResult> RegisterAsync(
        string email,
        string password,
        string fullName,
        string? phone,
        CancellationToken cancellationToken = default);
}

public interface ICustomerLoginService
{
    Task<CustomerLoginResult> LoginAsync(
        string email,
        string password,
        string? ipAddress,
        CancellationToken cancellationToken = default);
}

public interface ICustomerSignInService
{
    Task<(bool Succeeded, string? ErrorMessage)> SignInAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task SignOutAsync(CancellationToken cancellationToken = default);
}

public interface ICurrentCustomerAccessor
{
    Task<CustomerUserDto?> GetCurrentCustomerAsync(CancellationToken cancellationToken = default);
}

public interface ICustomerPasswordService
{
    Task<GenericMessageResult> ForgotPasswordAsync(string email, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? ErrorMessage)> ResetPasswordAsync(
        string email,
        string token,
        string newPassword,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? ErrorMessage)> ConfirmEmailAsync(
        string email,
        string token,
        CancellationToken cancellationToken = default);
}
