using System;
using System.Collections.Generic;
using CardCore;
using CardCore.Commands;
using CardCore.Events;
using Xunit;

namespace CardCore.PureTests;

public class PeekAccessorsTests
{
    private static CardInstance NewCard(string defId = "c") =>
        CardInstance.From(new CardDefinition(defId));

    private static List<CardInstance> SmallDeck() => new()
    {
        NewCard("a"), NewCard("b"), NewCard("c"),
    };

    [Fact]
    public void GetDeckCount_PreStart_Throws()
    {
        var engine = new GameEngine();
        Assert.Throws<InvalidOperationException>(() => engine.GetDeckCount(0));
    }

    [Fact]
    public void GetDiscardCount_PreStart_Throws()
    {
        var engine = new GameEngine();
        Assert.Throws<InvalidOperationException>(() => engine.GetDiscardCount(0));
    }

    [Fact]
    public void GetDeckCount_AfterStart_ReturnsDeckSize()
    {
        var engine = new GameEngine();
        engine.ExecuteCommand(new StartGameCommand(SmallDeck(), 1, 0));
        Assert.Equal(3, engine.GetDeckCount(0));
    }

    [Fact]
    public void GetDeckCount_AfterDraws_Decrements()
    {
        var engine = new GameEngine();
        engine.ExecuteCommand(new StartGameCommand(SmallDeck(), 1, 0));
        engine.ExecuteCommand(new DrawCardCommand(0));
        Assert.Equal(2, engine.GetDeckCount(0));
    }

    [Fact]
    public void GetDiscardCount_AfterStart_IsZero()
    {
        var engine = new GameEngine();
        engine.ExecuteCommand(new StartGameCommand(SmallDeck(), 1, 0));
        Assert.Equal(0, engine.GetDiscardCount(0));
    }

    [Fact]
    public void GetDiscardCount_AfterDiscardCommand_Increments()
    {
        var engine = new GameEngine();
        engine.ExecuteCommand(new StartGameCommand(SmallDeck(), 1, 0));
        engine.ExecuteCommand(new DrawCardCommand(0));
        var card = engine.GetCurrentState().Players[0].Hand[0];
        engine.ExecuteCommand(new DiscardCommand(0, card.InstanceId));
        Assert.Equal(1, engine.GetDiscardCount(0));
    }

    [Fact]
    public void GetDeckCount_InvalidPlayerId_Throws()
    {
        var engine = new GameEngine();
        engine.ExecuteCommand(new StartGameCommand(SmallDeck(), 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.GetDeckCount(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.GetDeckCount(99));
    }

    [Fact]
    public void GetDiscardCount_InvalidPlayerId_Throws()
    {
        var engine = new GameEngine();
        engine.ExecuteCommand(new StartGameCommand(SmallDeck(), 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.GetDiscardCount(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.GetDiscardCount(99));
    }
}
