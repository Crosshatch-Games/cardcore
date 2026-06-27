using System;
using System.Collections.Generic;
using CardCore.Commands;

namespace CardCore;

public sealed class GameEngine : IGameEngine
{
    private readonly List<GameEvent> _log = new();
    private readonly GameState _state = new();

    public IReadOnlyList<GameEvent> GetEventLog() => _log.AsReadOnly();

    public IReadOnlyList<GameEvent> ExecuteCommand(IGameCommand command)
    {
        if (command is null) throw new ArgumentNullException(nameof(command));
        if (!command.CanExecute(_state))
            throw new InvalidOperationException(
                $"Command {command.GetType().Name} failed CanExecute against current state.");

        var raw = command.Execute(_state);
        var stamped = new List<GameEvent>(raw.Count);
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        foreach (var evt in raw)
        {
            var withMeta = evt with { SequenceId = _log.Count, Timestamp = now };
            _log.Add(withMeta);
            _state.Apply(withMeta);
            stamped.Add(withMeta);
        }

        return stamped.AsReadOnly();
    }

    public GameState GetCurrentState() => _state.Clone();

    public GameState GetStateAtIndex(int eventIndex)
    {
        if (eventIndex < 0 || eventIndex >= _log.Count)
            throw new ArgumentOutOfRangeException(nameof(eventIndex));
        var s = new GameState();
        for (int i = 0; i <= eventIndex; i++)
            s.Apply(_log[i]);
        return s.Clone();
    }

    public void LoadEventLog(IReadOnlyList<GameEvent> events)
    {
        if (events is null) throw new ArgumentNullException(nameof(events));
        if (_log.Count > 0)
            throw new InvalidOperationException("Engine already has events; LoadEventLog requires a fresh engine.");
        if (events.Count == 0) return;

        // Validate before applying anything, so failure leaves engine pristine.
        if (events[0] is not Events.GameStarted)
            throw new InvalidOperationException("First event must be GameStarted.");
        for (int i = 0; i < events.Count; i++)
        {
            if (events[i].SequenceId != i)
                throw new InvalidOperationException(
                    $"Non-contiguous SequenceId at position {i}: expected {i}, got {events[i].SequenceId}.");
            if (i > 0 && events[i] is Events.GameStarted)
                throw new InvalidOperationException(
                    $"Duplicate GameStarted at SequenceId {events[i].SequenceId}.");
        }

        foreach (var evt in events)
        {
            _log.Add(evt);
            _state.Apply(evt);
        }
    }

    public int GetDeckCount(int playerId)
    {
        if (!_state.IsStarted)
            throw new InvalidOperationException("GetDeckCount requires the game to be started.");
        if (playerId < 0 || playerId >= _state.Players.Count)
            throw new ArgumentOutOfRangeException(nameof(playerId));
        return _state.Deck?.Count ?? 0;
    }

    public int GetDiscardCount(int playerId)
    {
        if (!_state.IsStarted)
            throw new InvalidOperationException("GetDiscardCount requires the game to be started.");
        if (playerId < 0 || playerId >= _state.Players.Count)
            throw new ArgumentOutOfRangeException(nameof(playerId));
        return _state.Players[playerId].DiscardPile.Count;
    }

    public void MutateLiveCardAction(Guid instanceId, int actionIndex, Action action)
    {
        if (action is null) throw new ArgumentNullException(nameof(action));

        for (int p = 0; p < _state.Players.Count; p++)
        {
            var hand = _state.Players[p].Hand;
            for (int i = 0; i < hand.Count; i++)
            {
                var card = hand[i];
                if (card.InstanceId == instanceId)
                {
                    // CardInstance.ReplaceAction handles index-range validation
                    // and throws ArgumentOutOfRangeException.
                    card.ReplaceAction(actionIndex, action);
                    return;
                }
            }
        }

        throw new InvalidOperationException(
            $"MutateLiveCardAction: card with InstanceId {instanceId} is not in any player's hand. " +
            "Only cards currently in hand can be mutated; cards in PlayArea, Deck, or DiscardPile are immutable.");
    }
}
