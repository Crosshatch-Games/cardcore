using System;
using System.Collections.Generic;
using CardCore;
using CardCore.Commands;
using CardCore.Events;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CardCore.PureTests;

public class RulesetEventTests
{
    private static CardInstance NewCard(string defId = "c") =>
        CardInstance.From(new CardDefinition(defId));

    private static GameEngine StartedEngine()
    {
        var engine = new GameEngine();
        engine.ExecuteCommand(new StartGameCommand(
            new List<CardInstance> { NewCard("a"), NewCard("b"), NewCard("c") }, 1, 7));
        return engine;
    }

    [Fact]
    public void Command_EmptyKind_Throws()
    {
        Assert.Throws<ArgumentException>(() => new EmitRulesetEventCommand("", new JObject()));
    }

    [Fact]
    public void Command_WhitespaceKind_Throws()
    {
        Assert.Throws<ArgumentException>(() => new EmitRulesetEventCommand("  ", new JObject()));
    }

    [Fact]
    public void Command_NullKind_Throws()
    {
        Assert.Throws<ArgumentException>(() => new EmitRulesetEventCommand(null!, new JObject()));
    }

    [Fact]
    public void Command_NullPayload_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new EmitRulesetEventCommand("tick", null!));
    }

    [Fact]
    public void CanExecute_GameNotStarted_False()
    {
        var cmd = new EmitRulesetEventCommand("tick", new JObject());
        Assert.False(cmd.CanExecute(new GameState()));
    }

    [Fact]
    public void Emit_AppendsEventToLog_WithKindAndPayload()
    {
        var engine = StartedEngine();
        var payload = new JObject { ["turn"] = 3, ["note"] = "dawn" };

        var emitted = engine.ExecuteCommand(new EmitRulesetEventCommand("turn-advanced", payload));

        var evt = Assert.IsType<RulesetEvent>(Assert.Single(emitted));
        Assert.Equal("turn-advanced", evt.Kind);
        Assert.True(JToken.DeepEquals(payload, evt.Payload));
        Assert.Equal(1, evt.SequenceId);
        Assert.Equal(2, engine.GetEventLog().Count);
        Assert.Same(evt, engine.GetEventLog()[1]);
    }

    [Fact]
    public void Emit_DoesNotAliasCallerPayload()
    {
        var engine = StartedEngine();
        var payload = new JObject { ["n"] = 1 };

        var emitted = engine.ExecuteCommand(new EmitRulesetEventCommand("k", payload));
        payload["n"] = 99;

        var evt = Assert.IsType<RulesetEvent>(emitted[0]);
        Assert.Equal(1, (int)evt.Payload["n"]!);
    }

    [Fact]
    public void Emit_Apply_IsNoOpOnGameState()
    {
        var engine = StartedEngine();
        var before = JsonConvert.SerializeObject(engine.GetCurrentState(), GameEvent.JsonSettings);

        engine.ExecuteCommand(new EmitRulesetEventCommand("tick", new JObject { ["x"] = 1 }));

        var after = JsonConvert.SerializeObject(engine.GetCurrentState(), GameEvent.JsonSettings);
        Assert.Equal(before, after);
    }

    [Fact]
    public void Emit_LogOrder_InterleavesWithCardEvents()
    {
        var engine = StartedEngine();
        engine.ExecuteCommand(new EmitRulesetEventCommand("first", new JObject()));
        engine.ExecuteCommand(new DrawCardCommand(0));
        engine.ExecuteCommand(new EmitRulesetEventCommand("second", new JObject()));

        var log = engine.GetEventLog();
        Assert.Equal(4, log.Count);
        Assert.IsType<GameStarted>(log[0]);
        Assert.Equal("first", Assert.IsType<RulesetEvent>(log[1]).Kind);
        Assert.IsType<CardDrawn>(log[2]);
        Assert.Equal("second", Assert.IsType<RulesetEvent>(log[3]).Kind);
        for (int i = 0; i < log.Count; i++) Assert.Equal(i, log[i].SequenceId);
    }

    [Fact]
    public void RulesetEvent_RoundTripsThroughJson()
    {
        var evt = new RulesetEvent
        {
            SequenceId = 4, Timestamp = 1234,
            Kind = "resources-granted",
            Payload = new JObject
            {
                ["gold"] = 5,
                ["nested"] = new JObject { ["a"] = new JArray(1, 2, 3) },
            },
        };

        var json = JsonConvert.SerializeObject(evt, typeof(GameEvent), GameEvent.JsonSettings);
        var rt = Assert.IsType<RulesetEvent>(JsonConvert.DeserializeObject<GameEvent>(json, GameEvent.JsonSettings));

        Assert.Equal(4, rt.SequenceId);
        Assert.Equal(1234, rt.Timestamp);
        Assert.Equal("resources-granted", rt.Kind);
        Assert.True(JToken.DeepEquals(evt.Payload, rt.Payload));
    }

    [Fact]
    public void LoadEventLog_ReplaysRulesetEvents()
    {
        var source = StartedEngine();
        source.ExecuteCommand(new EmitRulesetEventCommand("a", new JObject { ["v"] = 1 }));
        source.ExecuteCommand(new DrawCardCommand(0));
        source.ExecuteCommand(new EmitRulesetEventCommand("b", new JObject { ["v"] = 2 }));

        var json = JsonConvert.SerializeObject(source.GetEventLog(), GameEvent.JsonSettings);
        var loaded = JsonConvert.DeserializeObject<List<GameEvent>>(json, GameEvent.JsonSettings)!;

        var replay = new GameEngine();
        replay.LoadEventLog(loaded);

        var log = replay.GetEventLog();
        Assert.Equal(source.GetEventLog().Count, log.Count);
        Assert.Equal("a", Assert.IsType<RulesetEvent>(log[1]).Kind);
        Assert.Equal("b", Assert.IsType<RulesetEvent>(log[3]).Kind);
        Assert.Equal(
            JsonConvert.SerializeObject(source.GetCurrentState(), GameEvent.JsonSettings),
            JsonConvert.SerializeObject(replay.GetCurrentState(), GameEvent.JsonSettings));
    }

    [Fact]
    public void GetStateAtIndex_CountsRulesetEventsInIndexing()
    {
        var engine = StartedEngine();
        engine.ExecuteCommand(new EmitRulesetEventCommand("a", new JObject()));   // index 1
        engine.ExecuteCommand(new DrawCardCommand(0));                            // index 2

        Assert.Equal(0, engine.GetStateAtIndex(1).Players[0].Hand.Count);
        Assert.Equal(1, engine.GetStateAtIndex(2).Players[0].Hand.Count);
    }
}
