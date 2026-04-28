using CardCore;
using CardCore.Events;
using Xunit;

namespace CardCore.PureTests;

public class GameStateTests
{
    [Fact]
    public void NewState_IsNotStarted_AndEmpty()
    {
        var s = new GameState();
        Assert.False(s.IsStarted);
        Assert.Empty(s.Players);
        Assert.Empty(s.PlayArea);
        Assert.Null(s.Deck);
    }

    [Fact]
    public void Apply_GameStarted_SeedsState()
    {
        var s = new GameState();
        var deck = new List<Card> { new(1, "A"), new(2, "B"), new(3, "C") };
        var evt = new GameStarted
        {
            SequenceId = 0, Timestamp = 0,
            InitialDeckOrder = deck, PlayerCount = 2, Seed = 42,
        };

        s.ApplyForTest(evt);

        Assert.True(s.IsStarted);
        Assert.Equal(2, s.Players.Count);
        Assert.Equal(0, s.Players[0].Id);
        Assert.Equal(1, s.Players[1].Id);
        Assert.Empty(s.PlayArea);
        Assert.NotNull(s.Deck);
        Assert.Equal(3, s.Deck!.Count);
        Assert.Equal(42, s.Seed);
    }

    [Fact]
    public void Apply_GameStartedTwice_Throws()
    {
        var s = new GameState();
        var deck = new List<Card> { new(1, "A") };
        var evt = new GameStarted
        {
            SequenceId = 0, Timestamp = 0,
            InitialDeckOrder = deck, PlayerCount = 1, Seed = 0,
        };
        s.ApplyForTest(evt);

        Assert.Throws<InvalidOperationException>(() => s.ApplyForTest(evt with { SequenceId = 1 }));
    }

    private static GameState NewStartedState(int playerCount, params Card[] deck)
    {
        var s = new GameState();
        s.ApplyForTest(new GameStarted
        {
            SequenceId = 0, Timestamp = 0,
            InitialDeckOrder = deck, PlayerCount = playerCount, Seed = 0,
        });
        return s;
    }

    [Fact]
    public void Apply_CardDrawn_MovesTopOfDeckToHand()
    {
        var s = NewStartedState(1, new Card(1, "A"), new Card(2, "B"));

        s.ApplyForTest(new CardDrawn
        {
            SequenceId = 1, Timestamp = 0,
            PlayerId = 0, CardId = 1, DeckIndexBefore = 0,
        });

        Assert.Equal(1, s.Players[0].Hand.Count);
        Assert.Equal(1, s.Players[0].Hand[0].Id);
        Assert.Equal(1, s.Deck!.Count);
        Assert.Equal(2, s.Deck[0].Id);
    }

    [Fact]
    public void Apply_CardDrawn_OnEmptyDeck_Throws()
    {
        var s = NewStartedState(1);
        Assert.Throws<InvalidOperationException>(() => s.ApplyForTest(new CardDrawn
        {
            SequenceId = 1, Timestamp = 0,
            PlayerId = 0, CardId = 1, DeckIndexBefore = 0,
        }));
    }

    [Fact]
    public void Apply_CardPlayed_MovesCardFromHandToPlayArea()
    {
        var s = NewStartedState(1, new Card(1, "A"), new Card(2, "B"));
        s.ApplyForTest(new CardDrawn
        {
            SequenceId = 1, Timestamp = 0,
            PlayerId = 0, CardId = 1, DeckIndexBefore = 0,
        });

        s.ApplyForTest(new CardPlayed
        {
            SequenceId = 2, Timestamp = 0,
            PlayerId = 0, CardId = 1, HandIndexBefore = 0, PlayAreaIndexAfter = 0,
        });

        Assert.Equal(0, s.Players[0].Hand.Count);
        Assert.Equal(1, s.PlayArea.Count);
        Assert.Equal(1, s.PlayArea[0].Id);
    }

    [Fact]
    public void Apply_CardPlayed_HandIndexOutOfRange_Throws()
    {
        var s = NewStartedState(1, new Card(1, "A"));
        Assert.Throws<InvalidOperationException>(() => s.ApplyForTest(new CardPlayed
        {
            SequenceId = 1, Timestamp = 0,
            PlayerId = 0, CardId = 1, HandIndexBefore = 0, PlayAreaIndexAfter = 0,
        }));
    }
}
