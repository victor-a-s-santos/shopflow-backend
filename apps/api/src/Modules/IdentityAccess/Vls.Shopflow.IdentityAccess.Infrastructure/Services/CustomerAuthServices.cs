using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Vls.Shopflow.IdentityAccess.Application.DataTransferObjects;
using Vls.Shopflow.IdentityAccess.Application.Interfaces;
using Vls.Shopflow.IdentityAccess.Application.Security;
using Vls.Shopflow.IdentityAccess.Application.Services;
using Vls.Shopflow.IdentityAccess.Domain.Constants;
using Vls.Shopflow.IdentityAccess.Domain.Enums;
using Vls.Shopflow.IdentityAccess.Infrastructure.Identity;

namespace Vls.Shopflow.IdentityAccess.Infrastructure.Services;

public sealed class CustomerRegistrationService(
    UserManager<ShopflowUser> userManager,
    RoleManager<ShopflowRole> roleManager,
    IIdentityEmailSender emailSender,
    IStoreAccessPolicy storeAccessPolicy,
    ICustomerAccessNotifier customerAccessNotifier,
    ILogger<CustomerRegistrationService> logger)
    : ICustomerRegistrationService
{
    public async Task<RegisterCustomerResult> RegisterAsync(
        string email,
        string password,
        string fullName,
        string? phone,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim();

        var existing = await userManager.FindByEmailAsync(normalizedEmail);
        if (existing is not null)
        {
            return new RegisterCustomerResult(
                false,
                null,
                "Unable to complete registration.",
                IsDuplicateEmail: true,
                []);
        }

        if (!await roleManager.RoleExistsAsync(AuthRoles.Customer))
        {
            await roleManager.CreateAsync(new ShopflowRole { Name = AuthRoles.Customer });
        }

        var now = DateTimeOffset.UtcNow;
        var requireApproval = storeAccessPolicy.RequireApproval;
        var accessStatus = requireApproval
            ? CustomerAccessStatus.PendingApproval
            : CustomerAccessStatus.Approved;

        var user = new ShopflowUser
        {
            Id = Guid.NewGuid(),
            UserName = normalizedEmail,
            Email = normalizedEmail,
            EmailConfirmed = false,
            FullName = fullName.Trim(),
            PhoneNumber = phone?.Trim(),
            IsStaff = false,
            IsActive = true,
            CreatedAt = now,
            AccessStatus = accessStatus,
            AccessRequestedAt = now,
            ApprovedAt = requireApproval ? null : now
        };

        var createResult = await userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            var duplicate = createResult.Errors.Any(e =>
                e.Code is "DuplicateUserName" or "DuplicateEmail");
            if (duplicate)
            {
                return new RegisterCustomerResult(
                    false,
                    null,
                    "Unable to complete registration.",
                    IsDuplicateEmail: true,
                    []);
            }

            var fieldErrors = createResult.Errors
                .Select(MapIdentityError)
                .ToList();

            logger.LogWarning(
                "Customer registration failed for {Email}: {Errors}",
                normalizedEmail,
                string.Join("; ", createResult.Errors.Select(e => e.Description)));

            return new RegisterCustomerResult(
                false,
                null,
                fieldErrors.Count > 0
                    ? CustomerPasswordPolicy.SummaryMessage
                    : "Unable to complete registration.",
                IsDuplicateEmail: false,
                fieldErrors);
        }

        await userManager.AddToRoleAsync(user, AuthRoles.Customer);

        var confirmToken = await userManager.GenerateEmailConfirmationTokenAsync(user);
        await emailSender.SendEmailConfirmationAsync(normalizedEmail, confirmToken, cancellationToken);

        logger.LogInformation("Customer registered: {UserId} ({Email})", user.Id, normalizedEmail);

        var dto = await MapCustomerDtoAsync(user);
        var message = accessStatus == CustomerAccessStatus.PendingApproval
            ? CustomerAccessContract.RegisterPendingMessage
            : CustomerAccessContract.RegisterApprovedMessage;

        if (accessStatus == CustomerAccessStatus.PendingApproval)
        {
            try
            {
                await customerAccessNotifier.NotifyRegisteredPendingAsync(
                    new CustomerRegisteredPendingApproval(
                        user.Id,
                        normalizedEmail,
                        user.FullName ?? string.Empty,
                        now,
                        user.PhoneNumber),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to enqueue pending-approval e-mails for {UserId}",
                    user.Id);
            }
        }

        return new RegisterCustomerResult(true, dto, null, IsDuplicateEmail: false, [], message);
    }

    internal static RegisterCustomerFieldError MapIdentityError(IdentityError error)
    {
        var field = error.Code switch
        {
            "InvalidEmail" => "email",
            "DuplicateEmail" or "DuplicateUserName" => "email",
            "InvalidUserName" => "userName",
            var code when code.StartsWith("Password", StringComparison.Ordinal) => "password",
            _ => "password"
        };

        return new RegisterCustomerFieldError(
            field,
            CustomerPasswordPolicy.MapIdentityErrorCode(error.Code),
            CustomerPasswordPolicy.MapIdentityErrorMessage(error.Code, error.Description));
    }

    internal static async Task<CustomerUserDto> MapCustomerDtoAsync(UserManager<ShopflowUser> userManager, ShopflowUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        return new CustomerUserDto(
            user.Id,
            user.Email ?? string.Empty,
            user.FullName ?? string.Empty,
            user.PhoneNumber,
            user.EmailConfirmed,
            roles.ToList(),
            user.AccessStatus,
            user.AccessRequestedAt,
            user.ApprovedAt);
    }

    private Task<CustomerUserDto> MapCustomerDtoAsync(ShopflowUser user)
        => MapCustomerDtoAsync(userManager, user);
}

public sealed class CustomerLoginService(
    UserManager<ShopflowUser> userManager,
    ICustomerSignInService customerSignInService,
    ILogger<CustomerLoginService> logger)
    : ICustomerLoginService
{
    private const string GenericError = "Invalid email or password.";

    public async Task<CustomerLoginResult> LoginAsync(
        string email,
        string password,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim();
        var user = await userManager.FindByEmailAsync(normalizedEmail);

        if (user is null || !user.IsActive || user.IsStaff)
        {
            logger.LogWarning(
                "Customer login failed for {Email} from {IpAddress}: user not found or not eligible.",
                normalizedEmail,
                ipAddress ?? "unknown");
            return new CustomerLoginResult(false, null, GenericError);
        }

        if (!await userManager.IsInRoleAsync(user, AuthRoles.Customer))
        {
            logger.LogWarning(
                "Customer login failed for {Email} from {IpAddress}: missing Customer role.",
                normalizedEmail,
                ipAddress ?? "unknown");
            return new CustomerLoginResult(false, null, GenericError);
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            logger.LogWarning(
                "Customer login locked out for {Email} from {IpAddress}.",
                normalizedEmail,
                ipAddress ?? "unknown");
            return new CustomerLoginResult(false, null, "Account temporarily locked. Try again later.");
        }

        if (!await userManager.CheckPasswordAsync(user, password))
        {
            await userManager.AccessFailedAsync(user);
            logger.LogWarning(
                "Customer login failed for {Email} from {IpAddress}: invalid credentials.",
                normalizedEmail,
                ipAddress ?? "unknown");
            return new CustomerLoginResult(false, null, GenericError);
        }

        await userManager.ResetAccessFailedCountAsync(user);

        var (signInSucceeded, _) = await customerSignInService.SignInAsync(user.Id, cancellationToken);
        if (!signInSucceeded)
        {
            return new CustomerLoginResult(false, null, GenericError);
        }

        user.LastLoginAt = DateTimeOffset.UtcNow;
        await userManager.UpdateAsync(user);

        logger.LogInformation(
            "Customer login succeeded for {UserId} ({Email}) from {IpAddress}.",
            user.Id,
            user.Email,
            ipAddress ?? "unknown");

        var dto = await CustomerRegistrationService.MapCustomerDtoAsync(userManager, user);
        return new CustomerLoginResult(true, dto, null);
    }
}

public sealed class CustomerSignInService(
    SignInManager<ShopflowUser> signInManager,
    UserManager<ShopflowUser> userManager,
    IHttpContextAccessor httpContextAccessor)
    : ICustomerSignInService
{
    public async Task<(bool Succeeded, string? ErrorMessage)> SignInAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive || user.IsStaff)
            return (false, "Invalid email or password.");

        if (!await userManager.IsInRoleAsync(user, AuthRoles.Customer))
            return (false, "Invalid email or password.");

        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("HttpContext is not available.");

        var principal = await signInManager.CreateUserPrincipalAsync(user);
        await httpContext.SignInAsync(
            AuthSchemes.CustomerCookie,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = false,
                AllowRefresh = true
            });

        return (true, null);
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
            return;

        await httpContext.SignOutAsync(AuthSchemes.CustomerCookie);
    }
}

public sealed class CurrentCustomerAccessor(
    IHttpContextAccessor httpContextAccessor,
    UserManager<ShopflowUser> userManager)
    : ICurrentCustomerAccessor
{
    public async Task<CustomerUserDto?> GetCurrentCustomerAsync(CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
            return null;

        var authResult = await httpContext.AuthenticateAsync(AuthSchemes.CustomerCookie);
        if (!authResult.Succeeded || authResult.Principal is null)
            return null;

        var userIdClaim = authResult.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
            return null;

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive || user.IsStaff)
            return null;

        if (!await userManager.IsInRoleAsync(user, AuthRoles.Customer))
            return null;

        return await CustomerRegistrationService.MapCustomerDtoAsync(userManager, user);
    }
}

public sealed class CustomerPasswordService(
    UserManager<ShopflowUser> userManager,
    IIdentityEmailSender emailSender,
    ILogger<CustomerPasswordService> logger)
    : ICustomerPasswordService
{
    private const string ForgotPasswordMessage =
        "If the email is registered, we will send password reset instructions.";

    private const string GenericFailure = "Unable to complete the request.";

    public async Task<GenericMessageResult> ForgotPasswordAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim();
        var user = await userManager.FindByEmailAsync(normalizedEmail);

        if (user is not null
            && user.IsActive
            && !user.IsStaff
            && await userManager.IsInRoleAsync(user, AuthRoles.Customer))
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            try
            {
                await emailSender.SendPasswordResetAsync(normalizedEmail, token, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to enqueue password reset email.");
            }

            logger.LogInformation("Password reset requested for a matching customer account.");
        }

        return new GenericMessageResult(ForgotPasswordMessage);
    }

    public async Task<ResetCustomerPasswordResult> ResetPasswordAsync(
        string email,
        string token,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(email.Trim());
        if (user is null || user.IsStaff || !await userManager.IsInRoleAsync(user, AuthRoles.Customer))
            return new ResetCustomerPasswordResult(false, GenericFailure);

        var result = await userManager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
        {
            var passwordErrors = result.Errors
                .Where(e => e.Code.StartsWith("Password", StringComparison.Ordinal))
                .Select(CustomerRegistrationService.MapIdentityError)
                .ToList();

            logger.LogWarning(
                "Password reset failed for {Email}: {Errors}",
                email,
                string.Join("; ", result.Errors.Select(e => e.Description)));

            if (passwordErrors.Count > 0)
            {
                return new ResetCustomerPasswordResult(
                    false,
                    CustomerPasswordPolicy.SummaryMessage,
                    CustomerPasswordPolicy.TooWeakCode,
                    passwordErrors);
            }

            return new ResetCustomerPasswordResult(false, GenericFailure);
        }

        await userManager.UpdateSecurityStampAsync(user);
        return new ResetCustomerPasswordResult(true, null);
    }

    public async Task<(bool Succeeded, string? ErrorMessage)> ConfirmEmailAsync(
        string email,
        string token,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(email.Trim());
        if (user is null || user.IsStaff || !await userManager.IsInRoleAsync(user, AuthRoles.Customer))
            return (false, GenericFailure);

        var result = await userManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
        {
            logger.LogWarning(
                "Email confirmation failed for {Email}: {Errors}",
                email,
                string.Join("; ", result.Errors.Select(e => e.Description)));
            return (false, GenericFailure);
        }

        return (true, null);
    }
}
