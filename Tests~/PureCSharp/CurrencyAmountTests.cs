using System;
using CardCore;
using Xunit;

namespace CardCore.PureTests;

public class CurrencyAmountTests
{
    [Fact]
    public void Constructor_WithValidInput_SetsProperties()
    {
        var amount = new CurrencyAmount(3, "gold");

        Assert.Equal(3, amount.Amount);
        Assert.Equal("gold", amount.Type);
    }

    [Fact]
    public void Constructor_WithZeroAmount_Allowed()
    {
        var amount = new CurrencyAmount(0, "gold");
        Assert.Equal(0, amount.Amount);
    }

    [Fact]
    public void Constructor_WithNegativeAmount_Allowed()
    {
        var amount = new CurrencyAmount(-2, "gold");
        Assert.Equal(-2, amount.Amount);
    }

    [Fact]
    public void Constructor_WithNullType_Throws()
    {
        Assert.Throws<ArgumentException>(() => new CurrencyAmount(1, null!));
    }

    [Fact]
    public void Constructor_WithEmptyType_Throws()
    {
        Assert.Throws<ArgumentException>(() => new CurrencyAmount(1, ""));
    }

    [Fact]
    public void Constructor_WithWhitespaceType_Throws()
    {
        Assert.Throws<ArgumentException>(() => new CurrencyAmount(1, "   "));
    }

    [Fact]
    public void Equality_SameAmountAndType_AreEqual()
    {
        var a = new CurrencyAmount(2, "gold");
        var b = new CurrencyAmount(2, "gold");
        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void Equality_DifferentType_AreNotEqual()
    {
        var a = new CurrencyAmount(2, "gold");
        var b = new CurrencyAmount(2, "silver");
        Assert.NotEqual(a, b);
    }
}
