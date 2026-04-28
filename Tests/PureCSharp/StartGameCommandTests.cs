using CardCore;
using CardCore.Commands;
using CardCore.Events;
using Xunit;

namespace CardCore.PureTests;

public class StartGameCommandTests
{
    private static List<Card> ThreeCards() => new()
    {
        new Card(1, "A"), new Card(2, "B"), new Card(3, "C"),
    };

    [Fact]
    public void Constructor_NullDeck_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new StartGameCommand(null!, playerCount: 2, seed: 0));
    }

    [Fact]
    public void Constructor_EmptyDeck_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new StartGameCommand(new List<Card>(), playerCount: 2, seed: 0));
    }

    [Fact]
    public void Constructor_DuplicateCardIds_Throws()
    {
        var dup = new List<Card> { new(1, "A"), new(1, "B") };
        Assert.Throws<ArgumentException>(() =>
            new StartGameCommand(dup, playerCount: 2, seed: 0));
    }

    [Fact]
    public void Constructor_ZeroPlayers_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new StartGameCommand(ThreeCards(), playerCount: 0, seed: 0));
    }

    [Fact]
    public void CanExecute_OnEmptyState_True()
    {
        var cmd = new StartGameCommand(ThreeCards(), 2, 0);
        Assert.True(cmd.CanExecute(new GameState()));
    }

    [Fact]
    public void CanExecute_OnStartedState_False()
    {
        var s = new GameState();
        s.ApplyForTest(new GameStarted
        {
            SequenceId = 0, Timestamp = 0,
            InitialDeckOrder = ThreeCards(), PlayerCount = 1, Seed = 0,
        });

        var cmd = new StartGameCommand(ThreeCards(), 2, 0);
        Assert.False(cmd.CanExecute(s));
    }

    [Fact]
    public void Execute_EmitsSingleGameStartedEvent_WithShuffledOrder()
    {
        var cmd = new StartGameCommand(ThreeCards(), playerCount: 2, seed: 42);

        var events = cmd.Execute(new GameState());

        Assert.Single(events);
        var started = Assert.IsType<GameStarted>(events[0]);
        Assert.Equal(2, started.PlayerCount);
        Assert.Equal(42, started.Seed);
        Assert.Equal(3, started.InitialDeckOrder.Count);
    }

    [Fact]
    public void Execute_SameSeed_ProducesSameOrder()
    {
        var cmd1 = new StartGameCommand(ThreeCards(), 2, 42);
        var cmd2 = new StartGameCommand(ThreeCards(), 2, 42);

        var s1 = (GameStarted)cmd1.Execute(new GameState())[0];
        var s2 = (GameStarted)cmd2.Execute(new GameState())[0];

        Assert.Equal(s1.InitialDeckOrder.Select(c => c.Id),
                     s2.InitialDeckOrder.Select(c => c.Id));
    }
}
