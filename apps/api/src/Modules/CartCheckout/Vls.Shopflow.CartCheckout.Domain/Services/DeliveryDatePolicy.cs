namespace Vls.Shopflow.CartCheckout.Domain.Services;

/// <summary>
/// Preferred delivery date rules (MVP: Mon–Fri business days; no holidays).
/// </summary>
public static class DeliveryDatePolicy
{
    public const int MinimumBusinessDaysAfterPurchase = 2;

    public const string DeliveryDateTooSoonCode = "DELIVERY_DATE_TOO_SOON";

    public const string DeliveryDateTooSoonMessage =
        "A data preferida de entrega deve ser de pelo menos 2 dias úteis após a compra.";

    /// <summary>
    /// Advances <paramref name="startDate"/> by <paramref name="businessDays"/> weekdays
    /// (Saturday/Sunday skipped). Does not count the start date itself.
    /// </summary>
    public static DateOnly AddBusinessDays(DateOnly startDate, int businessDays)
    {
        if (businessDays < 0)
            throw new ArgumentOutOfRangeException(nameof(businessDays), "Business days cannot be negative.");

        var date = startDate;
        var added = 0;
        while (added < businessDays)
        {
            date = date.AddDays(1);
            if (IsBusinessDay(date))
                added++;
        }

        return date;
    }

    public static DateOnly GetMinimumPreferredDeliveryDate(DateOnly purchaseDate)
        => AddBusinessDays(purchaseDate, MinimumBusinessDaysAfterPurchase);

    public static bool IsValidPreferredDeliveryDate(DateOnly purchaseDate, DateOnly preferredDate)
        => preferredDate >= GetMinimumPreferredDeliveryDate(purchaseDate);

    public static bool IsBusinessDay(DateOnly date)
        => date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
}
