using System;
using System.Collections.Generic;
using CardCore.Events;

namespace CardCore.Commands;

public sealed class MoveDiscardToDeckCommand : IGameCommand
{
    private readonly int _playerId;

    public MoveDiscardToDeckCommand(int playerId)
    {
        if (playerId < 0)
            throw new ArgumentException("playerId must be >= 0.", nameof(playerId));
        _playerId = playerId;
    }

    public bool CanExecute(GameState state)
    {
        if (!state.IsStarted) return false;
        if (_playerId < 0 || _playerId >= state.Players.Count) return false;
        if (state.Deck is null || state.Deck.Count != 0) return false;
        if (state.Players[_playerId].DiscardPile.Count == 0) return false;
        return true;
    }

    public IReadOnlyList<GameEvent> Execute(GameState state)
    {
        var pile = state.Players[_playerId].DiscardPile;
        var ids = new List<Guid>(pile.Count);
        for (int i = 0; i < pile.Count; i++)
        {
            ids.Add(pile[i].InstanceId);
        }
        return new GameEvent[]
        {
            new DiscardMovedToDeck
            {
                PlayerId = _playerId,
                InstanceIds = ids,
            }
        };
    }
}
