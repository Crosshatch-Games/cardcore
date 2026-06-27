using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.Commands;
using CardCore.Events;
using Newtonsoft.Json.Linq;
using Xunit;
using Action = CardCore.Action;

namespace CardCore.PureTests;

public class MutateLiveCardActionTests
{
    private static CardInstance NewCardWithAction(string defId, string verb)
    {
        var def = new CardDefinition(
            Id: defId,
            Actions: new[] { new Action(verb, new JObject()) });
        return CardInstance.From(def);
    }

    private static Action MutatedAction(string verb, string payloadKey, string payloadValue)
    {
        var payload = new JObject { [payloadKey] = payloadValue };
        return new Action(verb, payload);
    }

    // Helper: build an engine started with a single hand card.
    private static (GameEngine engine, CardInstance card) StartedWithCardInHand(string defId = "x", string verb = "spawn_piece")
    {
        var card = NewCardWithAction(defId, verb);
        var engine = new GameEngine();
        engine.ExecuteCommand(new StartGameCommand(
            deck: new List<CardInstance> { card },
            playerCount: 1,
            seed: 0));
        engine.ExecuteCommand(new DrawCardCommand(0));
        return (engine, card);
    }

    [Fact]
    public void MutateLiveCardAction_OnHandCard_PersistsAcrossClone()
    {
        var (engine, card) = StartedWithCardInHand();
        var mutated = MutatedAction("spawn_piece", "position", "1,2,3");

        engine.MutateLiveCardAction(card.InstanceId, 0, mutated);

        var state = engine.GetCurrentState();
        var clonedCard = state.Players[0].Hand[0];
        Assert.Equal("1,2,3", (string?)clonedCard.Actions[0].Payload["position"]);
    }

    [Fact]
    public void MutateLiveCardAction_OnHandCard_PersistsAcrossPlay()
    {
        var (engine, card) = StartedWithCardInHand();
        var mutated = MutatedAction("spawn_piece", "position", "4,5,6");

        engine.MutateLiveCardAction(card.InstanceId, 0, mutated);
        engine.ExecuteCommand(new PlayCardCommand(0, 0));

        var state = engine.GetCurrentState();
        Assert.Single(state.PlayArea);
        Assert.Equal("4,5,6", (string?)state.PlayArea[0].Actions[0].Payload["position"]);
    }

    [Fact]
    public void MutateLiveCardAction_OnHandCard_PersistsAcrossRoundTrip()
    {
        var (engine, card) = StartedWithCardInHand();
        var mutated = MutatedAction("spawn_piece", "position", "7,8,9");

        engine.MutateLiveCardAction(card.InstanceId, 0, mutated);
        engine.ExecuteCommand(new PlayCardCommand(0, 0));

        var log = engine.GetEventLog();
        var fresh = new GameEngine();
        fresh.LoadEventLog(log.ToList());

        var rebuilt = fresh.GetCurrentState();
        Assert.Single(rebuilt.PlayArea);
        Assert.Equal("7,8,9", (string?)rebuilt.PlayArea[0].Actions[0].Payload["position"]);
    }

    [Fact]
    public void MutateLiveCardAction_OnPlayedCard_Throws()
    {
        var (engine, card) = StartedWithCardInHand();
        engine.ExecuteCommand(new PlayCardCommand(0, 0));

        var mutated = MutatedAction("spawn_piece", "position", "0,0,0");
        Assert.Throws<InvalidOperationException>(() =>
            engine.MutateLiveCardAction(card.InstanceId, 0, mutated));
    }

    [Fact]
    public void MutateLiveCardAction_OnDeckCard_Throws()
    {
        // Card still in deck (no DrawCardCommand fired).
        var deckCard = NewCardWithAction("y", "spawn_piece");
        var engine = new GameEngine();
        engine.ExecuteCommand(new StartGameCommand(
            deck: new List<CardInstance> { deckCard },
            playerCount: 1,
            seed: 0));

        var mutated = MutatedAction("spawn_piece", "position", "0,0,0");
        Assert.Throws<InvalidOperationException>(() =>
            engine.MutateLiveCardAction(deckCard.InstanceId, 0, mutated));
    }

    [Fact]
    public void MutateLiveCardAction_NotFound_Throws()
    {
        var (engine, _) = StartedWithCardInHand();
        var mutated = MutatedAction("spawn_piece", "position", "0,0,0");

        Assert.Throws<InvalidOperationException>(() =>
            engine.MutateLiveCardAction(Guid.NewGuid(), 0, mutated));
    }

    [Fact]
    public void MutateLiveCardAction_NullAction_Throws()
    {
        var (engine, card) = StartedWithCardInHand();

        Assert.Throws<ArgumentNullException>(() =>
            engine.MutateLiveCardAction(card.InstanceId, 0, null!));
    }

    [Fact]
    public void MutateLiveCardAction_IndexOutOfRange_Throws()
    {
        var (engine, card) = StartedWithCardInHand();
        var mutated = MutatedAction("spawn_piece", "position", "0,0,0");

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            engine.MutateLiveCardAction(card.InstanceId, -1, mutated));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            engine.MutateLiveCardAction(card.InstanceId, 5, mutated));
    }
}
