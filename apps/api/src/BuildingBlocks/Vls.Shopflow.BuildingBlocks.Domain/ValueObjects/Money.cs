namespace Vls.Shopflow.BuildingBlocks.Domain.ValueObjects;

public sealed record Money(decimal Amount, string Currency = "BRL") : ValueObject
{
    public static Money ZeroBRL => new(0m, "BRL");
    public static Money From(decimal amount) => new(amount, "BRL");

    public static Money operator +(Money a, Money b)
        => a.Currency == b.Currency ? new(a.Amount + b.Amount, a.Currency) : throw new InvalidOperationException();

    public static Money operator -(Money a, Money b)
        => a.Currency == b.Currency ? new(a.Amount - b.Amount, a.Currency) : throw new InvalidOperationException();
}