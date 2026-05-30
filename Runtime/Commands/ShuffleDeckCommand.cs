using System;
using System.Collections.Generic;
using CardCore.Events;

namespace CardCore.Commands;

public sealed class ShuffleDeckCommand : IGameCommand
{
    private readonly int _playerId;

    public ShuffleDeckCommand(int playerId)
    {
        if (playerId < 0)
            throw new ArgumentException("playerId must be >= 0.", nameof(playerId));
        _playerId = playerId;
    }

    public bool CanExecute(GameState state)
    {
        if (!state.IsStarted) return false;
        if (_playerId < 0 || _playerId >= state.Players.Count) return false;
        if (state.Deck is null || state.Deck.Count == 0) return false;
        return true;
    }

    public IReadOnlyList<GameEvent> Execute(GameState state)
    {
        var deck = state.Deck!;
        var ids = new List<Guid>(deck.Count);
        for (int i = 0; i < deck.Count; i++)
        {
            ids.Add(deck[i].InstanceId);
        }

        var rng = new Random();
        for (int i = ids.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (ids[i], ids[j]) = (ids[j], ids[i]);
        }

        return new GameEvent[]
        {
            new DeckShuffled
            {
                PlayerId = _playerId,
                PostShuffleInstanceIds = ids,
            }
        };
    }
}
