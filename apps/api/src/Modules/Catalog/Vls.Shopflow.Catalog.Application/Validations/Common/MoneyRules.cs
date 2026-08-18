namespace Vls.Shopflow.Catalog.Application.Validations.Common;

internal static class MoneyRules
{
    public const int MaxDecimalPlaces = 2;

    public static bool HasAtMostTwoDecimalPlaces(decimal value)
        => decimal.Round(value, MaxDecimalPlaces, MidpointRounding.AwayFromZero) == value;

    public static bool IsFinite(decimal value)
        => value is not (decimal.MinValue or decimal.MaxValue);
}
