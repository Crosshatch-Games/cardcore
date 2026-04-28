using System.Text.Json;
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

        var json = JsonSerializer.Serialize<GameEvent>(evt);
        var roundTrip = JsonSerializer.Deserialize<GameEvent>(json);

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
        var json = JsonSerializer.Serialize<GameEvent>(evt);
        var rt = Assert.IsType<CardDrawn>(JsonSerializer.Deserialize<GameEvent>(json));
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
        var json = JsonSerializer.Serialize<GameEvent>(evt);
        var rt = Assert.IsType<CardPlayed>(JsonSerializer.Deserialize<GameEvent>(json));
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
        var json = JsonSerializer.Serialize<GameEvent>(evt);
        Assert.Contains("\"$type\":\"CardDrawn\"", json);
    }
}
