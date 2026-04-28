namespace CardCore.Commands;

public interface IGameCommand
{
    bool CanExecute(GameState state);
    IReadOnlyList<GameEvent> Execute(GameState state);
}
