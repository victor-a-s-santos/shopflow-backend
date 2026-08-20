using FluentValidation;
using Vls.Shopflow.IdentityAccess.Application.Commands;
using Vls.Shopflow.IdentityAccess.Application.Queries;
using Vls.Shopflow.IdentityAccess.Application.Security;

namespace Vls.Shopflow.IdentityAccess.Application.Validators;

public sealed class RegisterCustomerCommandValidator : AbstractValidator<RegisterCustomerCommand>
{
    public RegisterCustomerCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).ApplyStrongPasswordRules();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Phone).MaximumLength(32).When(x => x.Phone is not null);
    }
}

public sealed class LoginCustomerCommandValidator : AbstractValidator<LoginCustomerCommand>
{
    public LoginCustomerCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public sealed class ForgotCustomerPasswordCommandValidator : AbstractValidator<ForgotCustomerPasswordCommand>
{
    public ForgotCustomerPasswordCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}

public sealed class ResetCustomerPasswordCommandValidator : AbstractValidator<ResetCustomerPasswordCommand>
{
    public ResetCustomerPasswordCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.NewPassword)
            .ApplyStrongPasswordRules()
            .WithName("newPassword");
    }
}

public sealed class ConfirmCustomerEmailCommandValidator : AbstractValidator<ConfirmCustomerEmailCommand>
{
    public ConfirmCustomerEmailCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Token).NotEmpty();
    }
}

public sealed class GetAdminCustomersQueryValidator : AbstractValidator<GetAdminCustomersQuery>
{
    public GetAdminCustomersQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Search).MaximumLength(256).When(x => x.Search is not null);
    }
}

public sealed class ApproveCustomerCommandValidator : AbstractValidator<ApproveCustomerCommand>
{
    public ApproveCustomerCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.AdminUserId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(512).When(x => x.Reason is not null);
    }
}

public sealed class RejectCustomerCommandValidator : AbstractValidator<RejectCustomerCommand>
{
    public RejectCustomerCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.AdminUserId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(512).When(x => x.Reason is not null);
    }
}

public sealed class SuspendCustomerCommandValidator : AbstractValidator<SuspendCustomerCommand>
{
    public SuspendCustomerCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.AdminUserId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(512).When(x => x.Reason is not null);
    }
}

public sealed class ReactivateCustomerCommandValidator : AbstractValidator<ReactivateCustomerCommand>
{
    public ReactivateCustomerCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.AdminUserId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(512).When(x => x.Reason is not null);
    }
}
