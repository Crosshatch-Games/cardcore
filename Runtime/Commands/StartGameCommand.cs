using System;
using System.Collections.Generic;
using CardCore.Events;

namespace CardCore.Commands;

public sealed class StartGameCommand : IGameCommand
{
    private readonly IReadOnlyList<CardInstance> _deck;
    private readonly int _playerCount;
    private readonly int _seed;

    public StartGameCommand(IReadOnlyList<CardInstance> deck, int playerCount, int seed)
    {
        if (deck is null) throw new ArgumentNullException(nameof(deck));
        if (deck.Count == 0)
            throw new ArgumentException("Deck must be non-empty.", nameof(deck));
        if (playerCount < 1)
            throw new ArgumentException("Player count must be >= 1.", nameof(playerCount));
        var ids = new HashSet<Guid>();
        foreach (var c in deck)
        {
            if (c is null)
                throw new ArgumentException("Deck must not contain null cards.", nameof(deck));
            if (!ids.Add(c.InstanceId))
                throw new ArgumentException(
                    $"Duplicate CardInstance id {c.InstanceId} in deck.", nameof(deck));
        }

        _deck = deck;
        _playerCount = playerCount;
        _seed = seed;
    }

    public bool CanExecute(GameState state) => !state.IsStarted;

    public IReadOnlyList<GameEvent> Execute(GameState state)
    {
        var rng = new Random(_seed);
        var shuffled = new List<CardInstance>(_deck);
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        return new GameEvent[]
        {
            new GameStarted
            {
                InitialDeckOrder = shuffled,
                PlayerCount = _playerCount,
                Seed = _seed,
            }
        };
    }
}
