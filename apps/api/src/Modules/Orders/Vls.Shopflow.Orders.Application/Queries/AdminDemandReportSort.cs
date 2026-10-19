namespace Vls.Shopflow.Orders.Application.Queries;

public static class AdminDemandReportSort
{
    public const string QuantityDesc = "quantity_desc";
    public const string QuantityAsc = "quantity_asc";
    public const string OrdersDesc = "orders_desc";
    public const string OrdersAsc = "orders_asc";
    public const string SubtotalDesc = "subtotal_desc";
    public const string SubtotalAsc = "subtotal_asc";

    public const string PaidAtDesc = "paidAt_desc";
    public const string PaidAtAsc = "paidAt_asc";
    public const string CreatedAtDesc = "createdAt_desc";
    public const string CreatedAtAsc = "createdAt_asc";

    public static readonly string[] ProductDemand =
    [
        QuantityDesc,
        QuantityAsc,
        OrdersDesc,
        OrdersAsc,
        SubtotalDesc,
        SubtotalAsc
    ];

    public static readonly string[] ProductDemandOrders =
    [
        PaidAtDesc,
        PaidAtAsc,
        CreatedAtDesc,
        CreatedAtAsc
    ];

    public static readonly string[] UnpaidOrders =
    [
        CreatedAtDesc,
        CreatedAtAsc
    ];

    public static bool IsAllowed(string? sort, IReadOnlyCollection<string> allowed)
    {
        if (string.IsNullOrWhiteSpace(sort))
            return true;

        var value = sort.Trim();
        foreach (var candidate in allowed)
        {
            if (string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static string Normalize(string? sort, string fallback)
        => string.IsNullOrWhiteSpace(sort) ? fallback : sort.Trim();
}
