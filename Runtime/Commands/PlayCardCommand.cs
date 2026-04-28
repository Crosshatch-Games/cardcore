using CardCore.Events;

namespace CardCore.Commands;

public sealed class PlayCardCommand : IGameCommand
{
    private readonly int _playerId;
    private readonly int _handIndex;

    public PlayCardCommand(int playerId, int handIndex)
    {
        if (playerId < 0)
            throw new ArgumentException("playerId must be >= 0.", nameof(playerId));
        if (handIndex < 0)
            throw new ArgumentException("handIndex must be >= 0.", nameof(handIndex));
        _playerId = playerId;
        _handIndex = handIndex;
    }

    public bool CanExecute(GameState state)
    {
        if (!state.IsStarted) return false;
        if (_playerId < 0 || _playerId >= state.Players.Count) return false;
        var hand = state.Players[_playerId].Hand;
        return _handIndex >= 0 && _handIndex < hand.Count;
    }

    public IReadOnlyList<GameEvent> Execute(GameState state)
    {
        var card = state.Players[_playerId].Hand[_handIndex];
        return new GameEvent[]
        {
            new CardPlayed
            {
                PlayerId = _playerId,
                CardId = card.Id,
                HandIndexBefore = _handIndex,
                PlayAreaIndexAfter = state.PlayArea.Count,
            }
        };
    }
}
