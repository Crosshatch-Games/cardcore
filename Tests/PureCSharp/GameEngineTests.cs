using CardCore;
using CardCore.Commands;
using CardCore.Events;
using Xunit;

namespace CardCore.PureTests;

public class GameEngineTests
{
    private static List<Card> SmallDeck() => new()
    {
        new Card(1, "A"), new Card(2, "B"), new Card(3, "C"),
    };

    [Fact]
    public void NewEngine_HasEmptyLog()
    {
        var engine = new GameEngine();
        Assert.Empty(engine.GetEventLog());
    }

    [Fact]
    public void ExecuteCommand_StartGame_AppendsAndApplies()
    {
        var engine = new GameEngine();
        var cmd = new StartGameCommand(SmallDeck(), 2, 42);

        var emitted = engine.ExecuteCommand(cmd);

        Assert.Single(emitted);
        Assert.Equal(1, engine.GetEventLog().Count);
        Assert.True(engine.GetCurrentState().IsStarted);
    }

    [Fact]
    public void ExecuteCommand_AssignsSequenceIdsContiguouslyFromZero()
    {
        var engine = new GameEngine();
        engine.ExecuteCommand(new StartGameCommand(SmallDeck(), 1, 0));
        engine.ExecuteCommand(new DrawCardCommand(0));

        var log = engine.GetEventLog();
        Assert.Equal(2, log.Count);
        Assert.Equal(0, log[0].SequenceId);
        Assert.Equal(1, log[1].SequenceId);
    }

    [Fact]
    public void ExecuteCommand_AssignsTimestampsMonotonically()
    {
        var engine = new GameEngine();
        engine.ExecuteCommand(new StartGameCommand(SmallDeck(), 1, 0));
        engine.ExecuteCommand(new DrawCardCommand(0));

        var log = engine.GetEventLog();
        Assert.True(log[1].Timestamp >= log[0].Timestamp);
    }

    [Fact]
    public void ExecuteCommand_FailsCanExecute_Throws()
    {
        var engine = new GameEngine();
        // DrawCardCommand against an unstarted state.
        Assert.Throws<InvalidOperationException>(
            () => engine.ExecuteCommand(new DrawCardCommand(0)));
    }

    [Fact]
    public void ThreeCommandSequence_ProducesExpectedState()
    {
        var engine = new GameEngine();
        engine.ExecuteCommand(new StartGameCommand(SmallDeck(), 1, 42));
        engine.ExecuteCommand(new DrawCardCommand(0));
        engine.ExecuteCommand(new PlayCardCommand(0, 0));

        var state = engine.GetCurrentState();
        Assert.Equal(0, state.Players[0].Hand.Count);
        Assert.Equal(1, state.PlayArea.Count);
        Assert.Equal(2, state.Deck!.Count);
    }

    [Fact]
    public void GetStateAtIndex_ZeroAfterStartGame_ReflectsStart()
    {
        var engine = new GameEngine();
        engine.ExecuteCommand(new StartGameCommand(SmallDeck(), 1, 0));

        var s = engine.GetStateAtIndex(0);
        Assert.True(s.IsStarted);
        Assert.Equal(3, s.Deck!.Count);
        Assert.Equal(0, s.Players[0].Hand.Count);
    }

    [Fact]
    public void GetStateAtIndex_AfterMultipleEvents_ReflectsExactPoint()
    {
        var engine = new GameEngine();
        engine.ExecuteCommand(new StartGameCommand(SmallDeck(), 1, 0));
        engine.ExecuteCommand(new DrawCardCommand(0)); // index 1
        engine.ExecuteCommand(new PlayCardCommand(0, 0)); // index 2

        var sAt1 = engine.GetStateAtIndex(1);
        Assert.Equal(1, sAt1.Players[0].Hand.Count);
        Assert.Equal(0, sAt1.PlayArea.Count);

        var sAt2 = engine.GetStateAtIndex(2);
        Assert.Equal(0, sAt2.Players[0].Hand.Count);
        Assert.Equal(1, sAt2.PlayArea.Count);
    }

    [Fact]
    public void GetStateAtIndex_OutOfRange_Throws()
    {
        var engine = new GameEngine();
        engine.ExecuteCommand(new StartGameCommand(SmallDeck(), 1, 0));

        Assert.Throws<ArgumentOutOfRangeException>(() => engine.GetStateAtIndex(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.GetStateAtIndex(99));
    }

    [Fact]
    public void GetCurrentState_ReturnsClone_NotLiveReference()
    {
        var engine = new GameEngine();
        engine.ExecuteCommand(new StartGameCommand(SmallDeck(), 1, 0));
        engine.ExecuteCommand(new DrawCardCommand(0));

        var s1 = engine.GetCurrentState();
        var s2 = engine.GetCurrentState();

        // Two clones, distinct instances.
        Assert.NotSame(s1, s2);
        // Mutating one (via internal hook) does not affect the other.
        s1.ApplyForTest(new CardPlayed
        {
            SequenceId = 99, Timestamp = 0,
            PlayerId = 0, CardId = s1.Players[0].Hand[0].Id,
            HandIndexBefore = 0, PlayAreaIndexAfter = 0,
        });
        Assert.Equal(1, s2.Players[0].Hand.Count);
    }

    [Fact]
    public void EventReplay_ReconstructsIdenticalState()
    {
        var engineA = new GameEngine();
        engineA.ExecuteCommand(new StartGameCommand(SmallDeck(), 1, 42));
        engineA.ExecuteCommand(new DrawCardCommand(0));
        engineA.ExecuteCommand(new PlayCardCommand(0, 0));
        engineA.ExecuteCommand(new DrawCardCommand(0));

        var json = System.Text.Json.JsonSerializer.Serialize(engineA.GetEventLog());
        var loaded = System.Text.Json.JsonSerializer.Deserialize<List<GameEvent>>(json)!;

        var engineB = new GameEngine();
        engineB.LoadEventLog(loaded);

        var jsonA = System.Text.Json.JsonSerializer.Serialize(engineA.GetCurrentState());
        var jsonB = System.Text.Json.JsonSerializer.Serialize(engineB.GetCurrentState());
        Assert.Equal(jsonA, jsonB);
    }

    [Fact]
    public void LoadEventLog_NullEvents_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new GameEngine().LoadEventLog(null!));
    }

    [Fact]
    public void LoadEventLog_FirstEventNotGameStarted_Throws()
    {
        var bogus = new List<GameEvent>
        {
            new CardDrawn { SequenceId = 0, Timestamp = 0, PlayerId = 0, CardId = 1, DeckIndexBefore = 0 },
        };
        Assert.Throws<InvalidOperationException>(() => new GameEngine().LoadEventLog(bogus));
    }

    [Fact]
    public void LoadEventLog_NonContiguousSequenceIds_Throws()
    {
        var deck = SmallDeck();
        var bogus = new List<GameEvent>
        {
            new GameStarted { SequenceId = 0, Timestamp = 0, InitialDeckOrder = deck, PlayerCount = 1, Seed = 0 },
            new CardDrawn { SequenceId = 5, Timestamp = 0, PlayerId = 0, CardId = deck[0].Id, DeckIndexBefore = 0 },
        };
        Assert.Throws<InvalidOperationException>(() => new GameEngine().LoadEventLog(bogus));
    }

    [Fact]
    public void LoadEventLog_DuplicateGameStarted_Throws()
    {
        var deck = SmallDeck();
        var bogus = new List<GameEvent>
        {
            new GameStarted { SequenceId = 0, Timestamp = 0, InitialDeckOrder = deck, PlayerCount = 1, Seed = 0 },
            new GameStarted { SequenceId = 1, Timestamp = 0, InitialDeckOrder = deck, PlayerCount = 1, Seed = 0 },
        };
        Assert.Throws<InvalidOperationException>(() => new GameEngine().LoadEventLog(bogus));
    }

    [Fact]
    public void LoadEventLog_OnEngineWithExistingLog_Throws()
    {
        var engine = new GameEngine();
        engine.ExecuteCommand(new StartGameCommand(SmallDeck(), 1, 0));

        var deck = SmallDeck();
        var more = new List<GameEvent>
        {
            new GameStarted { SequenceId = 0, Timestamp = 0, InitialDeckOrder = deck, PlayerCount = 1, Seed = 0 },
        };
        Assert.Throws<InvalidOperationException>(() => engine.LoadEventLog(more));
    }
}
