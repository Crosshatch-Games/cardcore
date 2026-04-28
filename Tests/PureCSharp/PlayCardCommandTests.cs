using CardCore;
using CardCore.Commands;
using CardCore.Events;
using Xunit;

namespace CardCore.PureTests;

public class PlayCardCommandTests
{
    private static GameState StartedWithCardInHand()
    {
        var s = new GameState();
        s.ApplyForTest(new GameStarted
        {
            SequenceId = 0, Timestamp = 0,
            InitialDeckOrder = new List<Card> { new(7, "X") },
            PlayerCount = 1, Seed = 0,
        });
        s.ApplyForTest(new CardDrawn
        {
            SequenceId = 1, Timestamp = 0,
            PlayerId = 0, CardId = 7, DeckIndexBefore = 0,
        });
        return s;
    }

    [Fact]
    public void Constructor_NegativePlayerId_Throws()
    {
        Assert.Throws<ArgumentException>(() => new PlayCardCommand(-1, 0));
    }

    [Fact]
    public void Constructor_NegativeHandIndex_Throws()
    {
        Assert.Throws<ArgumentException>(() => new PlayCardCommand(0, -1));
    }

    [Fact]
    public void CanExecute_GameNotStarted_False()
    {
        Assert.False(new PlayCardCommand(0, 0).CanExecute(new GameState()));
    }

    [Fact]
    public void CanExecute_HandIndexOutOfRange_False()
    {
        var s = StartedWithCardInHand();
        Assert.False(new PlayCardCommand(0, 5).CanExecute(s));
    }

    [Fact]
    public void CanExecute_Valid_True()
    {
        var s = StartedWithCardInHand();
        Assert.True(new PlayCardCommand(0, 0).CanExecute(s));
    }

    [Fact]
    public void Execute_EmitsSingleCardPlayedEvent()
    {
        var s = StartedWithCardInHand();
        var cmd = new PlayCardCommand(0, 0);

        var events = cmd.Execute(s);

        Assert.Single(events);
        var played = Assert.IsType<CardPlayed>(events[0]);
        Assert.Equal(0, played.PlayerId);
        Assert.Equal(7, played.CardId);
        Assert.Equal(0, played.HandIndexBefore);
        Assert.Equal(0, played.PlayAreaIndexAfter);
    }
}
