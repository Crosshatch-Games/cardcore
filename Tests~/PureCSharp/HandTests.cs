using System;
using CardCore;
using Xunit;

namespace CardCore.PureTests;

public class HandTests
{
    private static CardInstance NewCard(string defId = "c") =>
        CardInstance.From(new CardDefinition(defId));

    [Fact]
    public void NewHand_IsEmpty()
    {
        var hand = new Hand();
        Assert.Equal(0, hand.Count);
    }

    [Fact]
    public void Add_IncreasesCount()
    {
        var hand = new Hand();
        hand.Add(NewCard("a"));
        hand.Add(NewCard("b"));
        Assert.Equal(2, hand.Count);
    }

    [Fact]
    public void Indexer_ReturnsCardAtPosition()
    {
        var hand = new Hand();
        var a = NewCard("a");
        var b = NewCard("b");
        hand.Add(a);
        hand.Add(b);
        Assert.Equal(a, hand[0]);
        Assert.Equal(b, hand[1]);
    }

    [Fact]
    public void RemoveAt_ReturnsAndRemovesCard()
    {
        var hand = new Hand();
        var a = NewCard("a");
        var b = NewCard("b");
        hand.Add(a);
        hand.Add(b);

        var removed = hand.RemoveAt(0);

        Assert.Equal(a, removed);
        Assert.Equal(1, hand.Count);
        Assert.Equal(b, hand[0]);
    }

    [Fact]
    public void RemoveAt_OutOfRange_Throws()
    {
        var hand = new Hand();
        Assert.Throws<ArgumentOutOfRangeException>(() => hand.RemoveAt(0));
    }
}
