using Vls.Shopflow.Orders.Domain.Services;

namespace Vls.Shopflow.Orders.Application.Services;

/// <summary>
/// Digits-only phone for an admin to build a manual contact link.
/// Does not add a country code and does not invent missing digits.
/// </summary>
public static class AdminReportContact
{
    public const int MinPhoneDigits = 10;
    public const int MaxPhoneDigits = 15;

    public static string? PhoneDigits(string? phone)
    {
        var digits = CustomerContactNormalizer.NormalizePhone(phone);
        if (digits is null || digits.Length < MinPhoneDigits || digits.Length > MaxPhoneDigits)
            return null;

        return digits;
    }
}
