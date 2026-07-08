namespace Vls.Shopflow.Catalog.Domain.ValueObjects;

public sealed record PriceSummary(
    decimal Regular,
    decimal? Promotional,
    decimal Effective
);