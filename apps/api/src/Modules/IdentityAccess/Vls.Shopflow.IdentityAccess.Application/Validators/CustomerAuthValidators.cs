using FluentValidation;
using Vls.Shopflow.IdentityAccess.Application.Commands;

namespace Vls.Shopflow.IdentityAccess.Application.Validators;

public sealed class RegisterCustomerCommandValidator : AbstractValidator<RegisterCustomerCommand>
{
    public RegisterCustomerCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
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
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8);
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
