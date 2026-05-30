using System;
using System.Collections.Generic;
using CardCore.Events;

namespace CardCore.Commands;

public sealed class DiscardCommand : IGameCommand
{
    private readonly int _playerId;
    private readonly Guid _instanceId;

    public DiscardCommand(int playerId, Guid instanceId)
    {
        if (playerId < 0)
            throw new ArgumentException("playerId must be >= 0.", nameof(playerId));
        if (instanceId == Guid.Empty)
            throw new ArgumentException("instanceId must not be Guid.Empty.", nameof(instanceId));
        _playerId = playerId;
        _instanceId = instanceId;
    }

    public bool CanExecute(GameState state)
    {
        if (!state.IsStarted) return false;
        if (_playerId < 0 || _playerId >= state.Players.Count) return false;
        return FindHandIndex(state) >= 0;
    }

    public IReadOnlyList<GameEvent> Execute(GameState state)
    {
        int handIndex = FindHandIndex(state);
        return new GameEvent[]
        {
            new CardDiscarded
            {
                PlayerId = _playerId,
                InstanceId = _instanceId,
                HandIndexBefore = handIndex,
            }
        };
    }

    private int FindHandIndex(GameState state)
    {
        var hand = state.Players[_playerId].Hand;
        for (int i = 0; i < hand.Count; i++)
        {
            if (hand[i].InstanceId == _instanceId) return i;
        }
        return -1;
    }
}
