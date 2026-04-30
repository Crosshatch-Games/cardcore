using Newtonsoft.Json;
using CardCore;
using CardCore.Events;
using Xunit;

namespace CardCore.PureTests;

public class GameEventTests
{
    [Fact]
    public void GameStarted_RoundTripsThroughJson()
    {
        var deck = new List<Card> { new(1, "A"), new(2, "B") };
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
        Assert.Equal(1, typed.InitialDeckOrder[0].Id);
    }

    [Fact]
    public void CardDrawn_RoundTripsThroughJson()
    {
        var evt = new CardDrawn
        {
            SequenceId = 1, Timestamp = 1001,
            PlayerId = 0, CardId = 7, DeckIndexBefore = 3,
        };
        var json = JsonConvert.SerializeObject(evt, typeof(GameEvent), GameEvent.JsonSettings);
        var rt = Assert.IsType<CardDrawn>(JsonConvert.DeserializeObject<GameEvent>(json, GameEvent.JsonSettings));
        Assert.Equal(0, rt.PlayerId);
        Assert.Equal(7, rt.CardId);
        Assert.Equal(3, rt.DeckIndexBefore);
    }

    [Fact]
    public void CardPlayed_RoundTripsThroughJson()
    {
        var evt = new CardPlayed
        {
            SequenceId = 2, Timestamp = 1002,
            PlayerId = 1, CardId = 9,
            HandIndexBefore = 0, PlayAreaIndexAfter = 0,
        };
        var json = JsonConvert.SerializeObject(evt, typeof(GameEvent), GameEvent.JsonSettings);
        var rt = Assert.IsType<CardPlayed>(JsonConvert.DeserializeObject<GameEvent>(json, GameEvent.JsonSettings));
        Assert.Equal(1, rt.PlayerId);
        Assert.Equal(9, rt.CardId);
        Assert.Equal(0, rt.HandIndexBefore);
        Assert.Equal(0, rt.PlayAreaIndexAfter);
    }

    [Fact]
    public void DiscriminatorIsSimpleTypeName()
    {
        var evt = new CardDrawn
        {
            SequenceId = 0, Timestamp = 0,
            PlayerId = 0, CardId = 0, DeckIndexBefore = 0,
        };
        var json = JsonConvert.SerializeObject(evt, typeof(GameEvent), GameEvent.JsonSettings);
        Assert.Contains("\"$type\":\"CardDrawn\"", json);
    }
}
