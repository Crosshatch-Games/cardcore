using System;
using System.Collections.Generic;
using CardCore;
using CardCore.Events;
using Xunit;

namespace CardCore.PureTests;

public class GameStateTests
{
    private static CardInstance NewCard(string defId = "c") =>
        CardInstance.From(new CardDefinition(defId));

    [Fact]
    public void NewState_IsNotStarted_AndEmpty()
    {
        var s = new GameState();
        Assert.False(s.IsStarted);
        Assert.Empty(s.Players);
        Assert.Empty(s.PlayArea);
        Assert.Null(s.Deck);
    }

    [Fact]
    public void Apply_GameStarted_SeedsState()
    {
        var s = new GameState();
        var deck = new List<CardInstance> { NewCard("a"), NewCard("b"), NewCard("c") };
        var evt = new GameStarted
        {
            SequenceId = 0, Timestamp = 0,
            InitialDeckOrder = deck, PlayerCount = 2, Seed = 42,
        };

        s.ApplyForTest(evt);

        Assert.True(s.IsStarted);
        Assert.Equal(2, s.Players.Count);
        Assert.Equal(0, s.Players[0].Id);
        Assert.Equal(1, s.Players[1].Id);
        Assert.Empty(s.PlayArea);
        Assert.NotNull(s.Deck);
        Assert.Equal(3, s.Deck!.Count);
        Assert.Equal(42, s.Seed);
    }

    [Fact]
    public void Apply_GameStartedTwice_Throws()
    {
        var s = new GameState();
        var deck = new List<CardInstance> { NewCard("a") };
        var evt = new GameStarted
        {
            SequenceId = 0, Timestamp = 0,
            InitialDeckOrder = deck, PlayerCount = 1, Seed = 0,
        };
        s.ApplyForTest(evt);

        Assert.Throws<InvalidOperationException>(() => s.ApplyForTest(evt with { SequenceId = 1 }));
    }

    private static GameState NewStartedState(int playerCount, params CardInstance[] deck)
    {
        var s = new GameState();
        s.ApplyForTest(new GameStarted
        {
            SequenceId = 0, Timestamp = 0,
            InitialDeckOrder = deck, PlayerCount = playerCount, Seed = 0,
        });
        return s;
    }

    [Fact]
    public void Apply_CardDrawn_MovesTopOfDeckToHand()
    {
        var a = NewCard("a");
        var b = NewCard("b");
        var s = NewStartedState(1, a, b);

        s.ApplyForTest(new CardDrawn
        {
            SequenceId = 1, Timestamp = 0,
            PlayerId = 0, InstanceId = a.InstanceId, DeckIndexBefore = 0,
        });

        Assert.Single(s.Players[0].Hand.Cards);
        Assert.Equal(a.InstanceId, s.Players[0].Hand[0].InstanceId);
        Assert.Equal(1, s.Deck!.Count);
        Assert.Equal(b.InstanceId, s.Deck[0].InstanceId);
    }

    [Fact]
    public void Apply_CardDrawn_OnEmptyDeck_Throws()
    {
        var s = NewStartedState(1);
        Assert.Throws<InvalidOperationException>(() => s.ApplyForTest(new CardDrawn
        {
            SequenceId = 1, Timestamp = 0,
            PlayerId = 0, InstanceId = Guid.NewGuid(), DeckIndexBefore = 0,
        }));
    }

    [Fact]
    public void Apply_CardPlayed_MovesCardFromHandToPlayArea()
    {
        var a = NewCard("a");
        var b = NewCard("b");
        var s = NewStartedState(1, a, b);
        s.ApplyForTest(new CardDrawn
        {
            SequenceId = 1, Timestamp = 0,
            PlayerId = 0, InstanceId = a.InstanceId, DeckIndexBefore = 0,
        });

        s.ApplyForTest(new CardPlayed
        {
            SequenceId = 2, Timestamp = 0,
            PlayerId = 0, InstanceId = a.InstanceId, HandIndexBefore = 0, PlayAreaIndexAfter = 0,
        });

        Assert.Equal(0, s.Players[0].Hand.Count);
        Assert.Single(s.PlayArea);
        Assert.Equal(a.InstanceId, s.PlayArea[0].InstanceId);
    }

    [Fact]
    public void Apply_CardPlayed_HandIndexOutOfRange_Throws()
    {
        var s = NewStartedState(1, NewCard("a"));
        Assert.Throws<InvalidOperationException>(() => s.ApplyForTest(new CardPlayed
        {
            SequenceId = 1, Timestamp = 0,
            PlayerId = 0, InstanceId = Guid.NewGuid(), HandIndexBefore = 0, PlayAreaIndexAfter = 0,
        }));
    }

    [Fact]
    public void ApplyCardDiscarded_MovesCardFromHandToDiscardPile()
    {
        var card = NewCard("a");
        var s = new GameState();
        s.ApplyForTest(new GameStarted
        {
            SequenceId = 0, Timestamp = 0,
            InitialDeckOrder = new[] { card }, PlayerCount = 1, Seed = 0,
        });
        s.ApplyForTest(new CardDrawn
        {
            SequenceId = 1, Timestamp = 0,
            PlayerId = 0, InstanceId = card.InstanceId, DeckIndexBefore = 0,
        });

        s.ApplyForTest(new CardDiscarded
        {
            SequenceId = 2, Timestamp = 0,
            PlayerId = 0, InstanceId = card.InstanceId, HandIndexBefore = 0,
        });

        Assert.Equal(0, s.Players[0].Hand.Count);
        Assert.Equal(1, s.Players[0].DiscardPile.Count);
        Assert.Equal(card.InstanceId, s.Players[0].DiscardPile[0].InstanceId);
    }

    [Fact]
    public void ApplyCardDiscarded_InstanceIdMismatch_Throws()
    {
        var a = NewCard("a");
        var b = NewCard("b");
        var s = new GameState();
        s.ApplyForTest(new GameStarted
        {
            SequenceId = 0, Timestamp = 0,
            InitialDeckOrder = new[] { a, b }, PlayerCount = 1, Seed = 0,
        });
        s.ApplyForTest(new CardDrawn
        {
            SequenceId = 1, Timestamp = 0,
            PlayerId = 0, InstanceId = a.InstanceId, DeckIndexBefore = 0,
        });

        Assert.Throws<InvalidOperationException>(() => s.ApplyForTest(new CardDiscarded
        {
            SequenceId = 2, Timestamp = 0,
            PlayerId = 0, InstanceId = b.InstanceId, HandIndexBefore = 0, // wrong id at index 0
        }));
    }

    [Fact]
    public void ApplyCardDestroyed_RemovesCardFromHand_DiscardUntouched()
    {
        var card = NewCard("a");
        var s = new GameState();
        s.ApplyForTest(new GameStarted
        {
            SequenceId = 0, Timestamp = 0,
            InitialDeckOrder = new[] { card }, PlayerCount = 1, Seed = 0,
        });
        s.ApplyForTest(new CardDrawn
        {
            SequenceId = 1, Timestamp = 0,
            PlayerId = 0, InstanceId = card.InstanceId, DeckIndexBefore = 0,
        });

        s.ApplyForTest(new CardDestroyed
        {
            SequenceId = 2, Timestamp = 0,
            PlayerId = 0, InstanceId = card.InstanceId, HandIndexBefore = 0,
        });

        Assert.Equal(0, s.Players[0].Hand.Count);
        Assert.Equal(0, s.Players[0].DiscardPile.Count);
    }

    [Fact]
    public void ApplyDiscardMovedToDeck_DrainsPileIntoDeckInOrder()
    {
        var seed = NewCard("seed");
        var s = new GameState();
        s.ApplyForTest(new GameStarted
        {
            SequenceId = 0, Timestamp = 0,
            InitialDeckOrder = new[] { seed }, PlayerCount = 1, Seed = 0,
        });
        s.ApplyForTest(new CardDrawn
        {
            SequenceId = 1, Timestamp = 0,
            PlayerId = 0, InstanceId = seed.InstanceId, DeckIndexBefore = 0,
        });
        s.ApplyForTest(new CardPlayed
        {
            SequenceId = 2, Timestamp = 0,
            PlayerId = 0, InstanceId = seed.InstanceId,
            HandIndexBefore = 0, PlayAreaIndexAfter = 0,
        });
        var a = NewCard("a");
        var b = NewCard("b");
        s.Players[0].DiscardPile.Add(a);
        s.Players[0].DiscardPile.Add(b);

        s.ApplyForTest(new DiscardMovedToDeck
        {
            SequenceId = 3, Timestamp = 0,
            PlayerId = 0,
            InstanceIds = new List<Guid> { a.InstanceId, b.InstanceId },
        });

        Assert.Equal(0, s.Players[0].DiscardPile.Count);
        Assert.Equal(2, s.Deck!.Count);
        Assert.Equal(a.InstanceId, s.Deck[0].InstanceId);
        Assert.Equal(b.InstanceId, s.Deck[1].InstanceId);
    }

    [Fact]
    public void ApplyDiscardMovedToDeck_DeckNotEmpty_Throws()
    {
        var seed = NewCard("seed");
        var s = new GameState();
        s.ApplyForTest(new GameStarted
        {
            SequenceId = 0, Timestamp = 0,
            InitialDeckOrder = new[] { seed }, PlayerCount = 1, Seed = 0,
        });
        // Deck still has seed.
        s.Players[0].DiscardPile.Add(NewCard("a"));

        Assert.Throws<InvalidOperationException>(() => s.ApplyForTest(new DiscardMovedToDeck
        {
            SequenceId = 1, Timestamp = 0,
            PlayerId = 0,
            InstanceIds = new List<Guid> { s.Players[0].DiscardPile[0].InstanceId },
        }));
    }

    [Fact]
    public void ApplyDiscardMovedToDeck_IdSetMismatch_Throws()
    {
        var seed = NewCard("seed");
        var s = new GameState();
        s.ApplyForTest(new GameStarted
        {
            SequenceId = 0, Timestamp = 0,
            InitialDeckOrder = new[] { seed }, PlayerCount = 1, Seed = 0,
        });
        s.ApplyForTest(new CardDrawn
        {
            SequenceId = 1, Timestamp = 0,
            PlayerId = 0, InstanceId = seed.InstanceId, DeckIndexBefore = 0,
        });
        s.ApplyForTest(new CardPlayed
        {
            SequenceId = 2, Timestamp = 0,
            PlayerId = 0, InstanceId = seed.InstanceId,
            HandIndexBefore = 0, PlayAreaIndexAfter = 0,
        });
        s.Players[0].DiscardPile.Add(NewCard("a"));

        Assert.Throws<InvalidOperationException>(() => s.ApplyForTest(new DiscardMovedToDeck
        {
            SequenceId = 3, Timestamp = 0,
            PlayerId = 0,
            InstanceIds = new List<Guid> { Guid.NewGuid() }, // wrong id
        }));
    }

    [Fact]
    public void ApplyDeckShuffled_ReordersDeckToMatchEvent()
    {
        var a = NewCard("a");
        var b = NewCard("b");
        var c = NewCard("c");
        var s = new GameState();
        s.ApplyForTest(new GameStarted
        {
            SequenceId = 0, Timestamp = 0,
            InitialDeckOrder = new[] { a, b, c }, PlayerCount = 1, Seed = 0,
        });

        s.ApplyForTest(new DeckShuffled
        {
            SequenceId = 1, Timestamp = 0,
            PlayerId = 0,
            PostShuffleInstanceIds = new List<Guid> { c.InstanceId, a.InstanceId, b.InstanceId },
        });

        Assert.Equal(c.InstanceId, s.Deck![0].InstanceId);
        Assert.Equal(a.InstanceId, s.Deck[1].InstanceId);
        Assert.Equal(b.InstanceId, s.Deck[2].InstanceId);
    }

    [Fact]
    public void ApplyDeckShuffled_LengthMismatch_Throws()
    {
        var a = NewCard("a");
        var b = NewCard("b");
        var s = new GameState();
        s.ApplyForTest(new GameStarted
        {
            SequenceId = 0, Timestamp = 0,
            InitialDeckOrder = new[] { a, b }, PlayerCount = 1, Seed = 0,
        });

        Assert.Throws<InvalidOperationException>(() => s.ApplyForTest(new DeckShuffled
        {
            SequenceId = 1, Timestamp = 0,
            PlayerId = 0,
            PostShuffleInstanceIds = new List<Guid> { a.InstanceId }, // length 1, deck has 2
        }));
    }
}
