using System;
using System.Collections.Generic;
using CardCore;
using CardCore.Events;
using Newtonsoft.Json;
using Xunit;

namespace CardCore.PureTests;

public class GameEventTests
{
    private static CardInstance NewCard(string defId = "c") =>
        CardInstance.From(new CardDefinition(defId));

    [Fact]
    public void GameStarted_RoundTripsThroughJson()
    {
        var a = NewCard("a");
        var b = NewCard("b");
        var deck = new List<CardInstance> { a, b };
        var evt = new GameStarted
        {
            SequenceId = 0,
            Timestamp = 1000,
            InitialDeckOrder = deck,
            PlayerCount = 2,
            Seed = 42,
        };

        var json = JsonConvert.SerializeObject(evt, typeof(GameEvent), GameEvent.JsonSettings);
        var roundTrip = JsonConvert.DeserializeObject<GameEvent>(json, GameEvent.JsonSettings);

        var typed = Assert.IsType<GameStarted>(roundTrip);
        Assert.Equal(0, typed.SequenceId);
        Assert.Equal(1000, typed.Timestamp);
        Assert.Equal(2, typed.PlayerCount);
        Assert.Equal(42, typed.Seed);
        Assert.Equal(2, typed.InitialDeckOrder.Count);
        Assert.Equal(a.InstanceId, typed.InitialDeckOrder[0].InstanceId);
    }

    [Fact]
    public void CardDrawn_RoundTripsThroughJson()
    {
        var iid = Guid.NewGuid();
        var evt = new CardDrawn
        {
            SequenceId = 1, Timestamp = 1001,
            PlayerId = 0, InstanceId = iid, DeckIndexBefore = 3,
        };
        var json = JsonConvert.SerializeObject(evt, typeof(GameEvent), GameEvent.JsonSettings);
        var rt = Assert.IsType<CardDrawn>(JsonConvert.DeserializeObject<GameEvent>(json, GameEvent.JsonSettings));
        Assert.Equal(0, rt.PlayerId);
        Assert.Equal(iid, rt.InstanceId);
        Assert.Equal(3, rt.DeckIndexBefore);
    }

    [Fact]
    public void CardPlayed_RoundTripsThroughJson()
    {
        var iid = Guid.NewGuid();
        var evt = new CardPlayed
        {
            SequenceId = 2, Timestamp = 1002,
            PlayerId = 1, InstanceId = iid,
            HandIndexBefore = 0, PlayAreaIndexAfter = 0,
        };
        var json = JsonConvert.SerializeObject(evt, typeof(GameEvent), GameEvent.JsonSettings);
        var rt = Assert.IsType<CardPlayed>(JsonConvert.DeserializeObject<GameEvent>(json, GameEvent.JsonSettings));
        Assert.Equal(1, rt.PlayerId);
        Assert.Equal(iid, rt.InstanceId);
        Assert.Equal(0, rt.HandIndexBefore);
        Assert.Equal(0, rt.PlayAreaIndexAfter);
    }

    [Fact]
    public void DiscriminatorIsSimpleTypeName()
    {
        var evt = new CardDrawn
        {
            SequenceId = 0, Timestamp = 0,
            PlayerId = 0, InstanceId = Guid.NewGuid(), DeckIndexBefore = 0,
        };
        var json = JsonConvert.SerializeObject(evt, typeof(GameEvent), GameEvent.JsonSettings);
        Assert.Contains("\"$type\":\"CardDrawn\"", json);
    }
}
