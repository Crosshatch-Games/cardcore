using System;
using System.Linq;
using CardCore;
using CardCore.Markdown;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using Action = CardCore.Action;

namespace CardCore.PureTests;

public class CardInstanceTests
{
    private static CardDefinition SampleDefinition() => new CardDefinition(
        "reverie_muse",
        Name: MarkdownParser.Parse("reverie muse"),
        Types: new[] { "lifebound", "dream", "hero" },
        Costs: new[] { new CurrencyAmount(3, "[dream]") },
        Rewards: new[] { new CurrencyAmount(2, "[star]") },
        Thresholds: new[] { new CurrencyAmount(1, "[harvester]") },
        Actions: new[] { new Action("draw", new JObject()) },
        Targets: new[] { MarkdownParser.Parse("empty tableau slot") },
        Back: null,
        Rarity: "epic",
        Flavor: MarkdownParser.Parse("dreams of glory")
    );

    [Fact]
    public void From_CopiesAllFieldsFromDefinition()
    {
        var def = SampleDefinition();

        var inst = CardInstance.From(def);

        Assert.NotEqual(Guid.Empty, inst.InstanceId);
        Assert.Equal(def.Id, inst.DefinitionId);
        Assert.Equal(def.Name.Raw, inst.Name.Raw);
        Assert.Equal(def.Types, inst.Types);
        Assert.Equal(def.Costs, inst.Costs);
        Assert.Equal(def.Rewards, inst.Rewards);
        Assert.Equal(def.Thresholds, inst.Thresholds);
        Assert.Equal(def.Actions, inst.Actions);
        Assert.Equal(def.Rarity, inst.Rarity);
    }

    [Fact]
    public void From_TwoCallsProduceDistinctInstanceIds()
    {
        var def = SampleDefinition();
        var a = CardInstance.From(def);
        var b = CardInstance.From(def);
        Assert.NotEqual(a.InstanceId, b.InstanceId);
    }

    [Fact]
    public void AddAction_InsertsAtIndex()
    {
        var inst = CardInstance.From(SampleDefinition());
        var newAction = new Action("discard", new JObject());

        inst.AddAction(0, newAction);

        Assert.Equal(2, inst.Actions.Count);
        Assert.Equal(newAction, inst.Actions[0]);
    }

    [Fact]
    public void RemoveAction_RemovesAtIndex()
    {
        var inst = CardInstance.From(SampleDefinition());

        inst.RemoveAction(0);

        Assert.Empty(inst.Actions);
    }

    [Fact]
    public void ReplaceAction_SwapsAtIndex()
    {
        var inst = CardInstance.From(SampleDefinition());
        var replacement = new Action("end_turn", new JObject());

        inst.ReplaceAction(0, replacement);

        Assert.Single(inst.Actions);
        Assert.Equal(replacement, inst.Actions[0]);
    }

    [Fact]
    public void SetCost_OverwritesAtIndex()
    {
        var inst = CardInstance.From(SampleDefinition());
        var newCost = new CurrencyAmount(5, "[dream]");

        inst.SetCost(0, newCost);

        Assert.Equal(newCost, inst.Costs[0]);
    }

    [Fact]
    public void JsonRoundTrip_PreservesState()
    {
        var inst = CardInstance.From(SampleDefinition());
        inst.AddAction(0, new Action("discard", new JObject()));

        var json = JsonConvert.SerializeObject(inst, GameEvent.JsonSettings);
        var revived = JsonConvert.DeserializeObject<CardInstance>(json, GameEvent.JsonSettings)!;

        Assert.Equal(inst.InstanceId, revived.InstanceId);
        Assert.Equal(inst.DefinitionId, revived.DefinitionId);
        Assert.Equal(inst.Actions.Count, revived.Actions.Count);
        Assert.Equal(inst.Costs.Count, revived.Costs.Count);
    }
}
