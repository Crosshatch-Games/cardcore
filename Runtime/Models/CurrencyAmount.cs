using System;

namespace CardCore;

public readonly record struct CurrencyAmount
{
    public int Amount { get; }
    public string Type { get; }

    public CurrencyAmount(int Amount, string Type)
    {
        if (string.IsNullOrWhiteSpace(Type))
            throw new ArgumentException("CurrencyAmount.Type must be non-empty.", nameof(Type));
        this.Amount = Amount;
        this.Type = Type;
    }
}
