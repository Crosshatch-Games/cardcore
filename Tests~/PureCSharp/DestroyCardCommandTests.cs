using System;
using CardCore;
using CardCore.Commands;
using CardCore.Events;
using Xunit;

namespace CardCore.PureTests;

public class DestroyCardCommandTests
{
    private static CardInstance NewCard(string defId = "c") =>
        CardInstance.From(new CardDefinition(defId));

    private static GameState StartedStateWithHand(params CardInstance[] cards)
    {
        var s = new GameState();
        s.ApplyForTest(new GameStarted
        {
            SequenceId = 0, Timestamp = 0,
            InitialDeckOrder = cards, PlayerCount = 1, Seed = 0,
        });
        for (int i = 0; i < cards.Length; i++)
        {
            s.ApplyForTest(new CardDrawn
            {
                SequenceId = i + 1, Timestamp = 0,
                PlayerId = 0, InstanceId = cards[i].InstanceId, DeckIndexBefore = 0,
            });
        }
        return s;
    }

    [Fact]
    public void Constructor_NegativePlayerId_Throws()
    {
        Assert.Throws<ArgumentException>(() => new DestroyCardCommand(-1, Guid.NewGuid()));
    }

    [Fact]
    public void Constructor_EmptyGuid_Throws()
    {
        Assert.Throws<ArgumentException>(() => new DestroyCardCommand(0, Guid.Empty));
    }

    [Fact]
    public void CanExecute_GameNotStarted_False()
    {
        var cmd = new DestroyCardCommand(0, Guid.NewGuid());
        Assert.False(cmd.CanExecute(new GameState()));
    }

    [Fact]
    public void CanExecute_InvalidPlayerId_False()
    {
        var card = NewCard("a");
        var s = StartedStateWithHand(card);
        var cmd = new DestroyCardCommand(5, card.InstanceId);
        Assert.False(cmd.CanExecute(s));
    }

    [Fact]
    public void CanExecute_InstanceNotInHand_False()
    {
        var card = NewCard("a");
        var s = StartedStateWithHand(card);
        var cmd = new DestroyCardCommand(0, Guid.NewGuid());
        Assert.False(cmd.CanExecute(s));
    }

    [Fact]
    public void CanExecute_HappyPath_True()
    {
        var card = NewCard("a");
        var s = StartedStateWithHand(card);
        var cmd = new DestroyCardCommand(0, card.InstanceId);
        Assert.True(cmd.CanExecute(s));
    }

    [Fact]
    public void Execute_EmitsSingleCardDestroyedEvent_WithCorrectHandIndex()
    {
        var a = NewCard("a");
        var b = NewCard("b");
        var s = StartedStateWithHand(a, b);

        var cmd = new DestroyCardCommand(0, a.InstanceId);
        var events = cmd.Execute(s);

        Assert.Single(events);
        var typed = Assert.IsType<CardDestroyed>(events[0]);
        Assert.Equal(0, typed.PlayerId);
        Assert.Equal(a.InstanceId, typed.InstanceId);
        Assert.Equal(0, typed.HandIndexBefore);
    }
}
