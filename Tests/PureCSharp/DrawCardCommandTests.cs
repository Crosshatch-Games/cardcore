using CardCore;
using CardCore.Commands;
using CardCore.Events;
using Xunit;

namespace CardCore.PureTests;

public class DrawCardCommandTests
{
    private static GameState StartedState(int playerCount, params Card[] deck)
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
    public void Constructor_NegativePlayerId_Throws()
    {
        Assert.Throws<ArgumentException>(() => new DrawCardCommand(-1));
    }

    [Fact]
    public void CanExecute_GameNotStarted_False()
    {
        Assert.False(new DrawCardCommand(0).CanExecute(new GameState()));
    }

    [Fact]
    public void CanExecute_EmptyDeck_False()
    {
        var s = StartedState(1);
        Assert.False(new DrawCardCommand(0).CanExecute(s));
    }

    [Fact]
    public void CanExecute_InvalidPlayerId_False()
    {
        var s = StartedState(1, new Card(1, "A"));
        Assert.False(new DrawCardCommand(5).CanExecute(s));
    }

    [Fact]
    public void CanExecute_Valid_True()
    {
        var s = StartedState(1, new Card(1, "A"));
        Assert.True(new DrawCardCommand(0).CanExecute(s));
    }

    [Fact]
    public void Execute_EmitsSingleCardDrawnEvent_FromTopOfDeck()
    {
        var s = StartedState(1, new Card(7, "Top"), new Card(8, "Next"));
        var cmd = new DrawCardCommand(0);

        var events = cmd.Execute(s);

        Assert.Single(events);
        var drawn = Assert.IsType<CardDrawn>(events[0]);
        Assert.Equal(0, drawn.PlayerId);
        Assert.Equal(7, drawn.CardId);
        Assert.Equal(0, drawn.DeckIndexBefore);
    }
}
