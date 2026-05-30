using System;
using System.Collections.Generic;
using CardCore;
using Newtonsoft.Json;
using Xunit;

namespace CardCore.PureTests;

public class DiscardPileTests
{
    private static CardInstance NewCard(string defId = "c") =>
        CardInstance.From(new CardDefinition(defId));

    [Fact]
    public void Empty_HasZeroCount()
    {
        var pile = new DiscardPile();
        Assert.Equal(0, pile.Count);
        Assert.Empty(pile.Cards);
    }

    [Fact]
    public void Add_AppendsCard()
    {
        var pile = new DiscardPile();
        var card = NewCard("a");

        pile.Add(card);

        Assert.Equal(1, pile.Count);
        Assert.Same(card, pile[0]);
    }

    [Fact]
    public void Add_NullCard_Throws()
    {
        var pile = new DiscardPile();
        Assert.Throws<ArgumentNullException>(() => pile.Add(null!));
    }

    [Fact]
    public void RemoveAt_RemovesAndReturnsCard()
    {
        var pile = new DiscardPile();
        var a = NewCard("a");
        var b = NewCard("b");
        pile.Add(a);
        pile.Add(b);

        var removed = pile.RemoveAt(0);

        Assert.Same(a, removed);
        Assert.Equal(1, pile.Count);
        Assert.Same(b, pile[0]);
    }

    [Fact]
    public void RemoveAt_OutOfRange_Throws()
    {
        var pile = new DiscardPile();
        pile.Add(NewCard("a"));
        Assert.Throws<ArgumentOutOfRangeException>(() => pile.RemoveAt(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => pile.RemoveAt(5));
    }

    [Fact]
    public void AddRange_AppendsInOrder()
    {
        var pile = new DiscardPile();
        var a = NewCard("a");
        var b = NewCard("b");
        var c = NewCard("c");

        pile.AddRange(new List<CardInstance> { a, b, c });

        Assert.Equal(3, pile.Count);
        Assert.Same(a, pile[0]);
        Assert.Same(b, pile[1]);
        Assert.Same(c, pile[2]);
    }

    [Fact]
    public void AddRange_NullCollection_Throws()
    {
        var pile = new DiscardPile();
        Assert.Throws<ArgumentNullException>(() => pile.AddRange(null!));
    }

    [Fact]
    public void JsonRoundTrip_PreservesContents()
    {
        var pile = new DiscardPile();
        pile.Add(NewCard("a"));
        pile.Add(NewCard("b"));

        var json = JsonConvert.SerializeObject(pile, GameEvent.JsonSettings);
        var rehydrated = JsonConvert.DeserializeObject<DiscardPile>(json, GameEvent.JsonSettings)!;

        Assert.Equal(2, rehydrated.Count);
        Assert.Equal("a", rehydrated[0].DefinitionId);
        Assert.Equal("b", rehydrated[1].DefinitionId);
    }

    [Fact]
    public void JsonRoundTrip_Empty_Works()
    {
        var pile = new DiscardPile();
        var json = JsonConvert.SerializeObject(pile, GameEvent.JsonSettings);
        var rehydrated = JsonConvert.DeserializeObject<DiscardPile>(json, GameEvent.JsonSettings)!;

        Assert.Equal(0, rehydrated.Count);
    }
}
