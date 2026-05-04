using System;
using System.Collections.Generic;
using CardCore;
using Xunit;

namespace CardCore.PureTests;

public class DeckTests
{
    private static CardInstance NewCard(string defId = "c") =>
        CardInstance.From(new CardDefinition(defId));

    private static List<CardInstance> ThreeCards() => new()
    {
        NewCard("a"), NewCard("b"), NewCard("c"),
    };

    [Fact]
    public void NewDeck_HasCountEqualToInputCount()
    {
        var deck = new Deck(ThreeCards(), new Random(0));
        Assert.Equal(3, deck.Count);
    }

    [Fact]
    public void RemoveTop_DecrementsCount_AndReturnsTopCard()
    {
        var deck = new Deck(ThreeCards(), new Random(0));
        var topBefore = deck[0];

        var removed = deck.RemoveTop();

        Assert.Equal(topBefore, removed.Card);
        Assert.Equal(0, removed.IndexBefore);
        Assert.Equal(2, deck.Count);
    }

    [Fact]
    public void RemoveTop_OnEmptyDeck_Throws()
    {
        var deck = new Deck(new List<CardInstance>(), new Random(0));
        Assert.Throws<InvalidOperationException>(() => deck.RemoveTop());
    }

    [Fact]
    public void FindByInstanceId_ReturnsMatch()
    {
        var cards = ThreeCards();
        var target = cards[1];
        var deck = new Deck(cards, new Random(0));

        var card = deck.FindByInstanceId(target.InstanceId);
        Assert.Equal(target.InstanceId, card.InstanceId);
    }

    [Fact]
    public void FindByInstanceId_NoMatch_Throws()
    {
        var deck = new Deck(ThreeCards(), new Random(0));
        Assert.Throws<InvalidOperationException>(() => deck.FindByInstanceId(Guid.NewGuid()));
    }

    [Fact]
    public void Constructor_WithSameSeed_ProducesSameOrder()
    {
        var cards = ThreeCards();
        var d1 = new Deck(cards, new Random(42));
        var d2 = new Deck(cards, new Random(42));
        Assert.Equal(d1[0].InstanceId, d2[0].InstanceId);
        Assert.Equal(d1[1].InstanceId, d2[1].InstanceId);
        Assert.Equal(d1[2].InstanceId, d2[2].InstanceId);
    }

    [Fact]
    public void Constructor_NullCards_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new Deck(null!, new Random(0)));
    }
}
