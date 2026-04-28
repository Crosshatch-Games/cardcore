using CardCore.Commands;

namespace CardCore;

public interface IGameEngine
{
    IReadOnlyList<GameEvent> ExecuteCommand(IGameCommand command);
    IReadOnlyList<GameEvent> GetEventLog();
    GameState GetStateAtIndex(int eventIndex);
    GameState GetCurrentState();
    void LoadEventLog(IReadOnlyList<GameEvent> events);
}
