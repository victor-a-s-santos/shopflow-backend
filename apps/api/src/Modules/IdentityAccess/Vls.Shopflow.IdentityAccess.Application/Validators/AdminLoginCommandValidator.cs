using FluentValidation;
using Vls.Shopflow.IdentityAccess.Application.Commands;

namespace Vls.Shopflow.IdentityAccess.Application.Validators;

public sealed class AdminLoginCommandValidator : AbstractValidator<AdminLoginCommand>
{
    public AdminLoginCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty();
    }
}
