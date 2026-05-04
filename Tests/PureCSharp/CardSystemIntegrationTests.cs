using System;
using System.Collections.Generic;
using System.IO;
using CardCore;
using CardCore.Catalog;
using CardCore.Commands;
using CardCore.Events;
using Newtonsoft.Json;
using Xunit;

namespace CardCore.PureTests;

public class CardSystemIntegrationTests
{
    private static readonly string FixtureRoot =
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Cards");

    [Fact]
    public void CatalogToDeckToReplay_RoundTripsThroughJson()
    {
        // 1) Load catalog from JSON.
        var catalogJson = File.ReadAllText(Path.Combine(FixtureRoot, "integration_catalog.json"));
        var catalog = CardCatalogLoader.LoadFromJson(catalogJson);
        Assert.Equal(3, catalog.Count);

        // 2) Host builds a deck of CardInstances (two coppers, one silver, one gold).
        var copperDef = catalog.Get("copper");
        var silverDef = catalog.Get("silver");
        var goldDef = catalog.Get("gold");
        var deck = new List<CardInstance>
        {
            CardInstance.From(copperDef),
            CardInstance.From(copperDef),
            CardInstance.From(silverDef),
            CardInstance.From(goldDef),
        };

        // 3) Run Start/Draw/Play through the engine.
        var engineA = new GameEngine();
        engineA.ExecuteCommand(new StartGameCommand(deck, playerCount: 1, seed: 7));
        engineA.ExecuteCommand(new DrawCardCommand(0));
        engineA.ExecuteCommand(new DrawCardCommand(0));
        engineA.ExecuteCommand(new PlayCardCommand(0, 0));

        // 4) Round-trip the event log through JSON.
        var logJson = JsonConvert.SerializeObject(engineA.GetEventLog(), GameEvent.JsonSettings);
        var revivedLog = JsonConvert.DeserializeObject<List<GameEvent>>(logJson, GameEvent.JsonSettings)!;

        var engineB = new GameEngine();
        engineB.LoadEventLog(revivedLog);

        // 5) Final state of A and B serialize identically.
        var stateA = JsonConvert.SerializeObject(engineA.GetCurrentState(), GameEvent.JsonSettings);
        var stateB = JsonConvert.SerializeObject(engineB.GetCurrentState(), GameEvent.JsonSettings);
        Assert.Equal(stateA, stateB);

        // 6) The replayed engine has 1 card in the play area, hand has 1, deck has 2.
        var stB = engineB.GetCurrentState();
        Assert.Single(stB.PlayArea);
        Assert.Single(stB.Players[0].Hand.Cards);
        Assert.Equal(2, stB.Deck!.Count);

        // 7) Definition reference survived serialization.
        Assert.Equal("copper", stB.PlayArea[0].DefinitionId);
    }

    [Fact]
    public void CardInstance_WithRulesetMutations_SurvivesReplay()
    {
        var catalogJson = File.ReadAllText(Path.Combine(FixtureRoot, "integration_catalog.json"));
        var catalog = CardCatalogLoader.LoadFromJson(catalogJson);

        // Build a deck and mutate one instance before play.
        var copper = CardInstance.From(catalog.Get("copper"));
        copper.AddAction(0, new Action("foo", new Newtonsoft.Json.Linq.JObject()));

        var deck = new List<CardInstance> { copper };

        var engineA = new GameEngine();
        engineA.ExecuteCommand(new StartGameCommand(deck, playerCount: 1, seed: 0));
        engineA.ExecuteCommand(new DrawCardCommand(0));

        var logJson = JsonConvert.SerializeObject(engineA.GetEventLog(), GameEvent.JsonSettings);
        var revivedLog = JsonConvert.DeserializeObject<List<GameEvent>>(logJson, GameEvent.JsonSettings)!;

        var engineB = new GameEngine();
        engineB.LoadEventLog(revivedLog);

        // The mutated action should be present on the replayed instance.
        var revivedCopper = engineB.GetCurrentState().Players[0].Hand[0];
        Assert.Single(revivedCopper.Actions);
        Assert.Equal("foo", revivedCopper.Actions[0].Verb);
    }
}
