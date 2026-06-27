using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.Commands;
using CardCore.Events;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using Action = CardCore.Action;

namespace CardCore.PureTests;

public class CardPlayedActionsAtPlayTimeTests
{
    private static CardInstance NewCardWithAction(string defId, string verb)
    {
        var def = new CardDefinition(
            Id: defId,
            Actions: new[] { new Action(verb, new JObject()) });
        return CardInstance.From(def);
    }

    [Fact]
    public void PlayCardCommand_PopulatesActionsAtPlayTime_FromLiveCard()
    {
        var card = NewCardWithAction("x", "spawn_piece");
        var engine = new GameEngine();
        engine.ExecuteCommand(new StartGameCommand(
            deck: new List<CardInstance> { card },
            playerCount: 1,
            seed: 0));
        engine.ExecuteCommand(new DrawCardCommand(0));

        var mutated = new Action("spawn_piece", new JObject { ["position"] = "9,9,9" });
        engine.MutateLiveCardAction(card.InstanceId, 0, mutated);

        var emitted = engine.ExecuteCommand(new PlayCardCommand(0, 0));

        var played = Assert.IsType<CardPlayed>(emitted[0]);
        Assert.Single(played.ActionsAtPlayTime);
        Assert.Equal("9,9,9", (string?)played.ActionsAtPlayTime[0].Payload["position"]);
    }

    [Fact]
    public void OldShapeCardPlayed_WithoutActionsAtPlayTime_StillApplies()
    {
        // Simulate a pre-spec log: ActionsAtPlayTime is absent (deserializes to empty/default).
        // The engine must fall back to the in-hand card's current actions so old logs replay.
        var card = NewCardWithAction("x", "draw");
        var preSpecLogJson = JsonConvert.SerializeObject(new GameEvent[]
        {
            new GameStarted
            {
                SequenceId = 0, Timestamp = 0,
                InitialDeckOrder = new List<CardInstance> { card },
                PlayerCount = 1, Seed = 0,
            },
            new CardDrawn
            {
                SequenceId = 1, Timestamp = 0,
                PlayerId = 0, InstanceId = card.InstanceId, DeckIndexBefore = 0,
            },
            new CardPlayed
            {
                SequenceId = 2, Timestamp = 0,
                PlayerId = 0, InstanceId = card.InstanceId,
                HandIndexBefore = 0, PlayAreaIndexAfter = 0,
                // ActionsAtPlayTime intentionally left default (empty) — mimics an old log.
            },
        }, GameEvent.JsonSettings);

        var roundTripped = JsonConvert.DeserializeObject<GameEvent[]>(preSpecLogJson, GameEvent.JsonSettings)!;

        var engine = new GameEngine();
        engine.LoadEventLog(roundTripped);

        var state = engine.GetCurrentState();
        Assert.Single(state.PlayArea);
        // The card lands in PlayArea with its original (in-hand) actions, untouched.
        Assert.Equal("draw", state.PlayArea[0].Actions[0].Verb);
    }
}
