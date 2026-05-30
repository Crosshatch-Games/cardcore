using System.Collections.Generic;
using CardCore;
using CardCore.Commands;
using Newtonsoft.Json;
using Xunit;

namespace CardCore.PureTests;

public class ReshuffleRoundTripTests
{
    private static CardInstance NewCard(string defId) =>
        CardInstance.From(new CardDefinition(defId));

    [Fact]
    public void FullReshuffleCycle_ReplaysToIdenticalState()
    {
        var deckCards = new List<CardInstance>
        {
            NewCard("a"), NewCard("b"), NewCard("c"),
        };

        var engineA = new GameEngine();
        engineA.ExecuteCommand(new StartGameCommand(deckCards, 1, 42));

        // Draw the three cards.
        engineA.ExecuteCommand(new DrawCardCommand(0));
        engineA.ExecuteCommand(new DrawCardCommand(0));
        engineA.ExecuteCommand(new DrawCardCommand(0));

        // Discard all three (by id, captured fresh each time since hand shifts).
        var stateAfterDraws = engineA.GetCurrentState();
        var idsInHand = new List<System.Guid>();
        for (int i = 0; i < stateAfterDraws.Players[0].Hand.Count; i++)
            idsInHand.Add(stateAfterDraws.Players[0].Hand[i].InstanceId);
        foreach (var id in idsInHand)
            engineA.ExecuteCommand(new DiscardCommand(0, id));

        // Deck is empty, discard has 3 → reshuffle.
        engineA.ExecuteCommand(new MoveDiscardToDeckCommand(0));
        engineA.ExecuteCommand(new ShuffleDeckCommand(0));

        // Draw one card from the reshuffled deck.
        engineA.ExecuteCommand(new DrawCardCommand(0));

        var json = JsonConvert.SerializeObject(engineA.GetEventLog(), GameEvent.JsonSettings);
        var loaded = JsonConvert.DeserializeObject<List<GameEvent>>(json, GameEvent.JsonSettings)!;

        var engineB = new GameEngine();
        engineB.LoadEventLog(loaded);

        var jsonA = JsonConvert.SerializeObject(engineA.GetCurrentState(), GameEvent.JsonSettings);
        var jsonB = JsonConvert.SerializeObject(engineB.GetCurrentState(), GameEvent.JsonSettings);
        Assert.Equal(jsonA, jsonB);

        // And the counts match expectation.
        Assert.Equal(2, engineA.GetDeckCount(0));
        Assert.Equal(0, engineA.GetDiscardCount(0));
        Assert.Equal(1, engineA.GetCurrentState().Players[0].Hand.Count);
    }
}
