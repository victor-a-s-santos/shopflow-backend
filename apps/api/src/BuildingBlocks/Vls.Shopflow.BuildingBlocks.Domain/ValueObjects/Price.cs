namespace Vls.Shopflow.BuildingBlocks.Domain.ValueObjects;

public sealed record class Price : ValueObject
{
    public Money Regular { get; private set; } = default!;
    public Money? Promotional { get; private set; }
    public DateTimeOffset? PromoStart { get; private set; }
    public DateTimeOffset? PromoEnd { get; private set; }

    // ⬇️ EF precisa deste construtor para materializar o owned
    private Price()
    {
    }

    // Construtor de domínio (use na sua aplicação)
    public Price(Money regular, Money? promotional = null, DateTimeOffset? promoStart = null,
        DateTimeOffset? promoEnd = null)
    {
        Regular = regular ?? throw new ArgumentNullException(nameof(regular));
        Promotional = promotional;
        PromoStart = promoStart;
        PromoEnd = promoEnd;
    }

    // Preço efetivo agora (considera janela de promoção quando houver)
    public Money EffectiveNow(DateTimeOffset now)
    {
        if (Promotional is null) return Regular;

        var inWindow = (PromoStart is null || now >= PromoStart) &&
                       (PromoEnd is null || now <= PromoEnd);

        return inWindow && Promotional.Amount > 0 ? Promotional : Regular;
    }

    public static Price From(decimal regular, decimal? promotional = null,
        DateTimeOffset? start = null, DateTimeOffset? end = null)
        => new(Money.From(regular),
            promotional is null ? null : Money.From(promotional.Value),
            start, end);

    // helper: promoção inválida → ignora promo
    public Price Normalize()
        => Promotional is { Amount: <= 0 }
            ? this with { Promotional = null, PromoStart = null, PromoEnd = null }
            : this;

    public static Price Zero() =>
        new Price(Money.From(0), null, null, null);
}