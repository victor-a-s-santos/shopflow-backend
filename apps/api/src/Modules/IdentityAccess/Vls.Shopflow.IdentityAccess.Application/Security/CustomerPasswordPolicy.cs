using FluentValidation;

namespace Vls.Shopflow.IdentityAccess.Application.Security;

/// <summary>
/// Public customer password policy (register + reset). Backend source of truth alongside IdentityOptions.
/// </summary>
public static class CustomerPasswordPolicy
{
    public const int MinLength = 8;

    public const string TooWeakCode = "PASSWORD_TOO_WEAK";
    public const string TooShortCode = "PASSWORD_TOO_SHORT";
    public const string RequiresDigitCode = "PASSWORD_REQUIRES_DIGIT";
    public const string RequiresLowercaseCode = "PASSWORD_REQUIRES_LOWERCASE";
    public const string RequiresUppercaseCode = "PASSWORD_REQUIRES_UPPERCASE";
    public const string RequiresSpecialCode = "PASSWORD_REQUIRES_SPECIAL";

    public const string SummaryMessage =
        "A senha deve ter pelo menos 8 caracteres, incluindo letra maiúscula, letra minúscula, número e caractere especial.";

    public const string RequiredMessage = "A senha é obrigatória.";
    public const string TooShortMessage = "Use pelo menos 8 caracteres.";
    public const string RequiresDigitMessage = "Use pelo menos um número.";
    public const string RequiresLowercaseMessage = "Use pelo menos uma letra minúscula.";
    public const string RequiresUppercaseMessage = "Use pelo menos uma letra maiúscula.";
    public const string RequiresSpecialMessage = "Use pelo menos um caractere especial.";

    /// <summary>Dev/test example only — never a production secret.</summary>
    public const string DevTestExamplePassword = "Shopflow@123";

    public static bool HasDigit(string password) => password.Any(char.IsDigit);

    public static bool HasLowercase(string password) => password.Any(char.IsLower);

    public static bool HasUppercase(string password) => password.Any(char.IsUpper);

    public static bool HasSpecial(string password) => password.Any(c => !char.IsLetterOrDigit(c));

    public static IRuleBuilderOptions<T, string> ApplyStrongPasswordRules<T>(
        this IRuleBuilder<T, string> rule)
        => rule
            .NotEmpty()
            .WithMessage(RequiredMessage)
            .MinimumLength(MinLength)
            .WithMessage(TooShortMessage)
            .Must(HasDigit)
            .WithMessage(RequiresDigitMessage)
            .Must(HasLowercase)
            .WithMessage(RequiresLowercaseMessage)
            .Must(HasUppercase)
            .WithMessage(RequiresUppercaseMessage)
            .Must(HasSpecial)
            .WithMessage(RequiresSpecialMessage);

    public static string MapIdentityErrorCode(string identityCode)
        => identityCode switch
        {
            "PasswordTooShort" => TooShortCode,
            "PasswordRequiresDigit" => RequiresDigitCode,
            "PasswordRequiresLower" => RequiresLowercaseCode,
            "PasswordRequiresUpper" => RequiresUppercaseCode,
            "PasswordRequiresNonAlphanumeric" => RequiresSpecialCode,
            _ when identityCode.StartsWith("Password", StringComparison.Ordinal) => TooWeakCode,
            _ => TooWeakCode
        };

    public static string MapIdentityErrorMessage(string identityCode, string? description)
        => identityCode switch
        {
            "PasswordTooShort" => TooShortMessage,
            "PasswordRequiresDigit" => RequiresDigitMessage,
            "PasswordRequiresLower" => RequiresLowercaseMessage,
            "PasswordRequiresUpper" => RequiresUppercaseMessage,
            "PasswordRequiresNonAlphanumeric" => RequiresSpecialMessage,
            "PasswordRequiresUniqueChars" => "Use mais caracteres distintos na senha.",
            _ => string.IsNullOrWhiteSpace(description) ? SummaryMessage : description
        };
}
