using System;
using System.Collections.Generic;
using CardCore;
using CardCore.Commands;
using CardCore.Events;
using Xunit;

namespace CardCore.PureTests;

public class PlayCardCommandTests
{
    private static CardInstance NewCard(string defId = "c") =>
        CardInstance.From(new CardDefinition(defId));

    private static (GameState state, CardInstance card) StartedWithCardInHand()
    {
        var card = NewCard("x");
        var s = new GameState();
        s.ApplyForTest(new GameStarted
        {
            SequenceId = 0, Timestamp = 0,
            InitialDeckOrder = new List<CardInstance> { card },
            PlayerCount = 1, Seed = 0,
        });
        s.ApplyForTest(new CardDrawn
        {
            SequenceId = 1, Timestamp = 0,
            PlayerId = 0, InstanceId = card.InstanceId, DeckIndexBefore = 0,
        });
        return (s, card);
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
        var (s, _) = StartedWithCardInHand();
        Assert.False(new PlayCardCommand(0, 5).CanExecute(s));
    }

    [Fact]
    public void CanExecute_Valid_True()
    {
        var (s, _) = StartedWithCardInHand();
        Assert.True(new PlayCardCommand(0, 0).CanExecute(s));
    }

    [Fact]
    public void Execute_EmitsSingleCardPlayedEvent()
    {
        var (s, card) = StartedWithCardInHand();
        var cmd = new PlayCardCommand(0, 0);

        var events = cmd.Execute(s);

        Assert.Single(events);
        var played = Assert.IsType<CardPlayed>(events[0]);
        Assert.Equal(0, played.PlayerId);
        Assert.Equal(card.InstanceId, played.InstanceId);
        Assert.Equal(0, played.HandIndexBefore);
        Assert.Equal(0, played.PlayAreaIndexAfter);
    }
}
