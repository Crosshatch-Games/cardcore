using System.Collections.Generic;

namespace CardCore;

public interface IActionHandler
{
    string Verb { get; }
    IReadOnlyList<GameEvent> Handle(Action action, CardInstance card, GameState state);
}
