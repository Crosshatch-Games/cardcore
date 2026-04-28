using CardCore;
using Xunit;

namespace CardCore.PureTests;

public class DeckTests
{
    private static List<Card> ThreeCards() => new()
    {
        new Card(1, "A"),
        new Card(2, "B"),
        new Card(3, "C"),
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
        var deck = new Deck(new List<Card>(), new Random(0));
        Assert.Throws<InvalidOperationException>(() => deck.RemoveTop());
    }

    [Fact]
    public void FindCardById_ReturnsMatch()
    {
        var deck = new Deck(ThreeCards(), new Random(0));
        var card = deck.FindCardById(2);
        Assert.Equal(2, card.Id);
    }

    [Fact]
    public void FindCardById_NoMatch_Throws()
    {
        var deck = new Deck(ThreeCards(), new Random(0));
        Assert.Throws<InvalidOperationException>(() => deck.FindCardById(99));
    }

    [Fact]
    public void Constructor_WithSameSeed_ProducesSameOrder()
    {
        var d1 = new Deck(ThreeCards(), new Random(42));
        var d2 = new Deck(ThreeCards(), new Random(42));
        Assert.Equal(d1[0].Id, d2[0].Id);
        Assert.Equal(d1[1].Id, d2[1].Id);
        Assert.Equal(d1[2].Id, d2[2].Id);
    }

    [Fact]
    public void Constructor_NullCards_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new Deck(null!, new Random(0)));
    }
}
