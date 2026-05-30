using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.Commands;
using CardCore.Events;
using Xunit;

namespace CardCore.PureTests;

public class ShuffleDeckCommandTests
{
    private static CardInstance NewCard(string defId = "c") =>
        CardInstance.From(new CardDefinition(defId));

    private static GameState StartedStateWithDeck(params CardInstance[] deck)
    {
        var s = new GameState();
        s.ApplyForTest(new GameStarted
        {
            SequenceId = 0, Timestamp = 0,
            InitialDeckOrder = deck, PlayerCount = 1, Seed = 0,
        });
        return s;
    }

    [Fact]
    public void Constructor_NegativePlayerId_Throws()
    {
        Assert.Throws<ArgumentException>(() => new ShuffleDeckCommand(-1));
    }

    [Fact]
    public void CanExecute_GameNotStarted_False()
    {
        Assert.False(new ShuffleDeckCommand(0).CanExecute(new GameState()));
    }

    [Fact]
    public void CanExecute_InvalidPlayerId_False()
    {
        var s = StartedStateWithDeck(NewCard("a"));
        Assert.False(new ShuffleDeckCommand(5).CanExecute(s));
    }

    [Fact]
    public void CanExecute_EmptyDeck_False()
    {
        // GameStarted requires a non-empty deck, so seed with one card then drain it.
        var seedCard = NewCard("seed");
        var s = StartedStateWithDeck(seedCard);
        s.ApplyForTest(new CardDrawn
        {
            SequenceId = 1, Timestamp = 0,
            PlayerId = 0, InstanceId = seedCard.InstanceId, DeckIndexBefore = 0,
        });
        Assert.False(new ShuffleDeckCommand(0).CanExecute(s));
    }

    [Fact]
    public void CanExecute_DeckHasCards_True()
    {
        var s = StartedStateWithDeck(NewCard("a"), NewCard("b"));
        Assert.True(new ShuffleDeckCommand(0).CanExecute(s));
    }

    [Fact]
    public void Execute_EmitsDeckShuffled_WithSameIdSet()
    {
        var cards = new[]
        {
            NewCard("a"), NewCard("b"), NewCard("c"), NewCard("d"),
            NewCard("e"), NewCard("f"), NewCard("g"), NewCard("h"),
        };
        var s = StartedStateWithDeck(cards);

        var cmd = new ShuffleDeckCommand(0);
        var events = cmd.Execute(s);

        Assert.Single(events);
        var typed = Assert.IsType<DeckShuffled>(events[0]);
        Assert.Equal(0, typed.PlayerId);
        Assert.Equal(cards.Length, typed.PostShuffleInstanceIds.Count);

        var inputIds = new HashSet<Guid>(cards.Select(c => c.InstanceId));
        var outputIds = new HashSet<Guid>(typed.PostShuffleInstanceIds);
        Assert.Equal(inputIds, outputIds);
    }

    [Fact]
    public void Execute_MultipleRuns_ProduceDifferentOrderings()
    {
        // Probabilistic: with 18 cards and 5 trials, the chance of all 5 being
        // byte-identical to the input is ~ 5 * (1/18!), vanishingly small.
        var cards = new List<CardInstance>();
        for (int i = 0; i < 18; i++) cards.Add(NewCard($"c{i}"));
        var s = StartedStateWithDeck(cards.ToArray());
        var input = cards.Select(c => c.InstanceId).ToArray();
        var cmd = new ShuffleDeckCommand(0);

        bool sawDifferent = false;
        for (int trial = 0; trial < 5; trial++)
        {
            var evt = (DeckShuffled)cmd.Execute(s)[0];
            if (!evt.PostShuffleInstanceIds.SequenceEqual(input))
            {
                sawDifferent = true;
                break;
            }
        }
        Assert.True(sawDifferent, "5 shuffles produced the same ordering as input — RNG broken or shuffle is a no-op.");
    }
}
