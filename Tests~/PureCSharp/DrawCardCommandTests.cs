using System;
using CardCore;
using CardCore.Commands;
using CardCore.Events;
using Xunit;

namespace CardCore.PureTests;

public class DrawCardCommandTests
{
    private static CardInstance NewCard(string defId = "c") =>
        CardInstance.From(new CardDefinition(defId));

    private static GameState StartedState(int playerCount, params CardInstance[] deck)
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
        var s = StartedState(1, NewCard("a"));
        Assert.False(new DrawCardCommand(5).CanExecute(s));
    }

    [Fact]
    public void CanExecute_Valid_True()
    {
        var s = StartedState(1, NewCard("a"));
        Assert.True(new DrawCardCommand(0).CanExecute(s));
    }

    [Fact]
    public void Execute_EmitsSingleCardDrawnEvent_FromTopOfDeck()
    {
        var top = NewCard("top");
        var next = NewCard("next");
        var s = StartedState(1, top, next);
        var cmd = new DrawCardCommand(0);

        var events = cmd.Execute(s);

        Assert.Single(events);
        var drawn = Assert.IsType<CardDrawn>(events[0]);
        Assert.Equal(0, drawn.PlayerId);
        Assert.Equal(top.InstanceId, drawn.InstanceId);
        Assert.Equal(0, drawn.DeckIndexBefore);
    }
}
