using System;
using System.Collections.Generic;
using CardCore;
using CardCore.Commands;
using CardCore.Events;
using Xunit;

namespace CardCore.PureTests;

public class MoveDiscardToDeckCommandTests
{
    private static CardInstance NewCard(string defId = "c") =>
        CardInstance.From(new CardDefinition(defId));

    // Builds a started state with an empty deck and the given cards loaded
    // directly into the player's discard pile. Skips the engine entirely;
    // exercises only the apply paths that already exist.
    private static GameState StartedStateWithDiscardPile(params CardInstance[] discardContents)
    {
        var s = new GameState();
        // Start with one card so GameStarted has a non-empty deck (it requires non-empty).
        var seedCard = NewCard("seed");
        s.ApplyForTest(new GameStarted
        {
            SequenceId = 0, Timestamp = 0,
            InitialDeckOrder = new[] { seedCard }, PlayerCount = 1, Seed = 0,
        });
        // Drain the deck so it's empty.
        s.ApplyForTest(new CardDrawn
        {
            SequenceId = 1, Timestamp = 0,
            PlayerId = 0, InstanceId = seedCard.InstanceId, DeckIndexBefore = 0,
        });
        // Hand the seed card to the play area so it's not in hand.
        s.ApplyForTest(new CardPlayed
        {
            SequenceId = 2, Timestamp = 0,
            PlayerId = 0, InstanceId = seedCard.InstanceId,
            HandIndexBefore = 0, PlayAreaIndexAfter = 0,
        });
        // Manually populate the discard pile via its public API.
        foreach (var c in discardContents)
        {
            s.Players[0].DiscardPile.Add(c);
        }
        return s;
    }

    [Fact]
    public void Constructor_NegativePlayerId_Throws()
    {
        Assert.Throws<ArgumentException>(() => new MoveDiscardToDeckCommand(-1));
    }

    [Fact]
    public void CanExecute_GameNotStarted_False()
    {
        var cmd = new MoveDiscardToDeckCommand(0);
        Assert.False(cmd.CanExecute(new GameState()));
    }

    [Fact]
    public void CanExecute_InvalidPlayerId_False()
    {
        var s = StartedStateWithDiscardPile(NewCard("a"));
        var cmd = new MoveDiscardToDeckCommand(5);
        Assert.False(cmd.CanExecute(s));
    }

    [Fact]
    public void CanExecute_DeckNotEmpty_False()
    {
        var s = new GameState();
        var seedCard = NewCard("seed");
        s.ApplyForTest(new GameStarted
        {
            SequenceId = 0, Timestamp = 0,
            InitialDeckOrder = new[] { seedCard }, PlayerCount = 1, Seed = 0,
        });
        // Deck still has the seed card. Populate discard via public API.
        s.Players[0].DiscardPile.Add(NewCard("d"));

        var cmd = new MoveDiscardToDeckCommand(0);
        Assert.False(cmd.CanExecute(s));
    }

    [Fact]
    public void CanExecute_DiscardEmpty_False()
    {
        var s = StartedStateWithDiscardPile(/* no cards */);
        var cmd = new MoveDiscardToDeckCommand(0);
        Assert.False(cmd.CanExecute(s));
    }

    [Fact]
    public void CanExecute_HappyPath_True()
    {
        var s = StartedStateWithDiscardPile(NewCard("a"), NewCard("b"));
        var cmd = new MoveDiscardToDeckCommand(0);
        Assert.True(cmd.CanExecute(s));
    }

    [Fact]
    public void Execute_EmitsDiscardMovedToDeck_WithIdsInPileOrder()
    {
        var a = NewCard("a");
        var b = NewCard("b");
        var c = NewCard("c");
        var s = StartedStateWithDiscardPile(a, b, c);

        var cmd = new MoveDiscardToDeckCommand(0);
        var events = cmd.Execute(s);

        Assert.Single(events);
        var typed = Assert.IsType<DiscardMovedToDeck>(events[0]);
        Assert.Equal(0, typed.PlayerId);
        Assert.Equal(3, typed.InstanceIds.Count);
        Assert.Equal(a.InstanceId, typed.InstanceIds[0]);
        Assert.Equal(b.InstanceId, typed.InstanceIds[1]);
        Assert.Equal(c.InstanceId, typed.InstanceIds[2]);
    }
}
