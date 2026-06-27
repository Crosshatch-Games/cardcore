using System;
using System.Collections.Generic;
using CardCore.Commands;

namespace CardCore;

public interface IGameEngine
{
    IReadOnlyList<GameEvent> ExecuteCommand(IGameCommand command);
    IReadOnlyList<GameEvent> GetEventLog();
    GameState GetStateAtIndex(int eventIndex);
    GameState GetCurrentState();
    void LoadEventLog(IReadOnlyList<GameEvent> events);
    int GetDeckCount(int playerId);
    int GetDiscardCount(int playerId);

    /// <summary>
    /// Replace an action on the live CardInstance identified by <paramref name="instanceId"/>,
    /// in place, before the card is played. The mutation persists on the engine's working
    /// state so that the subsequent PlayCardCommand snapshots it into CardPlayed.ActionsAtPlayTime
    /// and replay via GetStateAtIndex / LoadEventLog reproduces the mutated payload.
    ///
    /// Validity:
    ///   - The card must currently reside in a player's Hand. Cards in PlayArea, Deck, or
    ///     DiscardPile cannot be mutated — once played, a card's actions are frozen in the log.
    ///   - <paramref name="actionIndex"/> must be in range [0, card.Actions.Count).
    ///   - <paramref name="action"/> must not be null.
    /// </summary>
    /// <exception cref="ArgumentNullException">action is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">actionIndex is out of range for the target card.</exception>
    /// <exception cref="InvalidOperationException">No card with the given InstanceId is currently in any player's hand.</exception>
    void MutateLiveCardAction(Guid instanceId, int actionIndex, Action action);
}
