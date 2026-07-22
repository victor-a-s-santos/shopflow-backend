using Microsoft.AspNetCore.Identity;
using Vls.Shopflow.IdentityAccess.Application.Interfaces;
using Vls.Shopflow.IdentityAccess.Infrastructure.Identity;
using Vls.Shopflow.Orders.Application.Interfaces;

namespace Vls.Shopflow.HttpApi.Services;

/// <summary>
/// Adapts IdentityAccess customer services for Orders guest-claim flows.
/// </summary>
public sealed class CustomerAccountPort(
    UserManager<ShopflowUser> userManager,
    ICustomerRegistrationService registrationService,
    ICustomerSignInService signInService)
    : ICustomerAccountPort
{
    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(email.Trim());
        return user is not null;
    }

    public async Task<CustomerAccountCreateResult> RegisterAsync(
        string email,
        string password,
        string fullName,
        string? phone,
        CancellationToken cancellationToken)
    {
        var result = await registrationService.RegisterAsync(
            email, password, fullName, phone, cancellationToken);

        if (result.IsDuplicateEmail)
        {
            return new CustomerAccountCreateResult(
                false,
                null,
                IsDuplicateEmail: true,
                []);
        }

        if (!result.Succeeded || result.Customer is null)
        {
            var errors = result.Errors.Count > 0
                ? result.Errors
                    .Select(e => new CustomerAccountFieldError(e.Field, e.Message))
                    .ToList()
                : [new CustomerAccountFieldError(
                    "password",
                    result.ErrorMessage ?? "A senha não atende aos requisitos.")];

            return new CustomerAccountCreateResult(
                false,
                null,
                IsDuplicateEmail: false,
                errors);
        }

        return new CustomerAccountCreateResult(
            true,
            result.Customer.CustomerId,
            IsDuplicateEmail: false,
            []);
    }

    public async Task SignInAsync(Guid customerUserId, CancellationToken cancellationToken)
    {
        var (succeeded, _) = await signInService.SignInAsync(customerUserId, cancellationToken);
        if (!succeeded)
        {
            // Account was created and order linked; session is best-effort.
            // Frontend can still login manually.
        }
    }
}
