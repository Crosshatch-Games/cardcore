using CardCore;
using Xunit;

namespace CardCore.PureTests;

public class PlayerTests
{
    [Fact]
    public void NewPlayer_HasIdAndEmptyHand()
    {
        var player = new Player(id: 0);
        Assert.Equal(0, player.Id);
        Assert.Equal(0, player.Hand.Count);
    }

    [Fact]
    public void NegativeId_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Player(id: -1));
    }

    [Fact]
    public void DefaultCtor_InitializesEmptyDiscardPile()
    {
        var player = new Player(0);
        Assert.NotNull(player.DiscardPile);
        Assert.Equal(0, player.DiscardPile.Count);
    }

    [Fact]
    public void JsonCtor_NullDiscardPile_InitializesEmpty()
    {
        // Simulates a legacy event log written before DiscardPile existed:
        // the JSON has no "DiscardPile" field, so it deserializes as null.
        var json = "{\"Id\":0,\"Hand\":{\"Cards\":[]}}";
        var player = Newtonsoft.Json.JsonConvert.DeserializeObject<Player>(
            json, CardCore.GameEvent.JsonSettings)!;

        Assert.NotNull(player.DiscardPile);
        Assert.Equal(0, player.DiscardPile.Count);
    }

    [Fact]
    public void JsonRoundTrip_PreservesDiscardPileContents()
    {
        var card = CardInstance.From(new CardDefinition("d"));
        var pile = new DiscardPile();
        pile.Add(card);
        var player = new Player(0, new Hand(), pile);

        var json = Newtonsoft.Json.JsonConvert.SerializeObject(player, CardCore.GameEvent.JsonSettings);
        var rehydrated = Newtonsoft.Json.JsonConvert.DeserializeObject<Player>(
            json, CardCore.GameEvent.JsonSettings)!;

        Assert.Equal(1, rehydrated.DiscardPile.Count);
        Assert.Equal("d", rehydrated.DiscardPile[0].DefinitionId);
    }
}
