using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.Commands;
using CardCore.Events;
using Xunit;

namespace CardCore.PureTests;

public class StartGameCommandTests
{
    private static CardInstance NewCard(string defId = "c") =>
        CardInstance.From(new CardDefinition(defId));

    private static List<CardInstance> ThreeCards() => new()
    {
        NewCard("a"), NewCard("b"), NewCard("c"),
    };

    [Fact]
    public void Constructor_NullDeck_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new StartGameCommand(null!, playerCount: 2, seed: 0));
    }

    [Fact]
    public void Constructor_EmptyDeck_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new StartGameCommand(new List<CardInstance>(), playerCount: 2, seed: 0));
    }

    [Fact]
    public void Constructor_DuplicateInstanceIds_Throws()
    {
        var card = NewCard("a");
        var dup = new List<CardInstance> { card, card };
        Assert.Throws<ArgumentException>(() =>
            new StartGameCommand(dup, playerCount: 2, seed: 0));
    }

    [Fact]
    public void Constructor_ZeroPlayers_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new StartGameCommand(ThreeCards(), playerCount: 0, seed: 0));
    }

    [Fact]
    public void CanExecute_OnEmptyState_True()
    {
        var cmd = new StartGameCommand(ThreeCards(), 2, 0);
        Assert.True(cmd.CanExecute(new GameState()));
    }

    [Fact]
    public void CanExecute_OnStartedState_False()
    {
        var s = new GameState();
        s.ApplyForTest(new GameStarted
        {
            SequenceId = 0, Timestamp = 0,
            InitialDeckOrder = ThreeCards(), PlayerCount = 1, Seed = 0,
        });

        var cmd = new StartGameCommand(ThreeCards(), 2, 0);
        Assert.False(cmd.CanExecute(s));
    }

    [Fact]
    public void Execute_EmitsSingleGameStartedEvent_WithShuffledOrder()
    {
        var cmd = new StartGameCommand(ThreeCards(), playerCount: 2, seed: 42);

        var events = cmd.Execute(new GameState());

        Assert.Single(events);
        var started = Assert.IsType<GameStarted>(events[0]);
        Assert.Equal(2, started.PlayerCount);
        Assert.Equal(42, started.Seed);
        Assert.Equal(3, started.InitialDeckOrder.Count);
    }

    [Fact]
    public void Execute_SameSeed_ProducesSameOrder()
    {
        var cards = ThreeCards();
        var cmd1 = new StartGameCommand(cards, 2, 42);
        var cmd2 = new StartGameCommand(cards, 2, 42);

        var s1 = (GameStarted)cmd1.Execute(new GameState())[0];
        var s2 = (GameStarted)cmd2.Execute(new GameState())[0];

        Assert.Equal(s1.InitialDeckOrder.Select(c => c.InstanceId),
                     s2.InitialDeckOrder.Select(c => c.InstanceId));
    }
}
