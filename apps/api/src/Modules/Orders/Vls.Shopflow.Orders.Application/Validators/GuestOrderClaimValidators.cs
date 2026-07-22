using FluentValidation;
using Vls.Shopflow.Orders.Application.Commands;

namespace Vls.Shopflow.Orders.Application.Validators;

public sealed class CreateAccountFromGuestOrderCommandValidator
    : AbstractValidator<CreateAccountFromGuestOrderCommand>
{
    public CreateAccountFromGuestOrderCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.GuestAccessToken)
            .NotEmpty()
            .WithMessage("Guest access token is required.")
            .WithName("guestAccessToken");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("A senha é obrigatória.")
            .MinimumLength(8)
            .WithMessage("Use pelo menos 8 caracteres.")
            .Must(p => p.Any(char.IsDigit))
            .WithMessage("Use pelo menos um número.")
            .Must(p => p.Any(char.IsLower))
            .WithMessage("Use pelo menos uma letra minúscula.")
            .WithName("password");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty()
            .Equal(x => x.Password)
            .WithMessage("A confirmação de senha não confere.")
            .WithName("confirmPassword");
    }
}

public sealed class ClaimGuestOrderCommandValidator : AbstractValidator<ClaimGuestOrderCommand>
{
    public ClaimGuestOrderCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.CustomerUserId).NotEmpty();
        RuleFor(x => x.CustomerEmail).NotEmpty().EmailAddress();
        RuleFor(x => x.GuestAccessToken)
            .NotEmpty()
            .WithMessage("Guest access token is required.")
            .WithName("guestAccessToken");
    }
}
