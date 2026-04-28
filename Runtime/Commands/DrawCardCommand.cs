using CardCore.Events;

namespace CardCore.Commands;

public sealed class DrawCardCommand : IGameCommand
{
    private readonly int _playerId;

    public DrawCardCommand(int playerId)
    {
        if (playerId < 0)
            throw new ArgumentException("playerId must be >= 0.", nameof(playerId));
        _playerId = playerId;
    }

    public bool CanExecute(GameState state)
    {
        if (!state.IsStarted) return false;
        if (state.Deck is null || state.Deck.Count == 0) return false;
        if (_playerId < 0 || _playerId >= state.Players.Count) return false;
        return true;
    }

    public IReadOnlyList<GameEvent> Execute(GameState state)
    {
        var top = state.Deck![0];
        return new GameEvent[]
        {
            new CardDrawn
            {
                PlayerId = _playerId,
                CardId = top.Id,
                DeckIndexBefore = 0,
            }
        };
    }
}
