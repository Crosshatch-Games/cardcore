using System;
using System.Collections.Generic;
using CardCore.Events;
using Newtonsoft.Json.Linq;

namespace CardCore.Commands;

public sealed class EmitRulesetEventCommand : IGameCommand
{
    private readonly string _kind;
    private readonly JObject _payload;

    public EmitRulesetEventCommand(string kind, JObject payload)
    {
        if (string.IsNullOrWhiteSpace(kind))
            throw new ArgumentException("kind must be non-empty.", nameof(kind));
        if (payload is null) throw new ArgumentNullException(nameof(payload));
        _kind = kind;
        // Copy so later mutation of the caller's JObject can't change the logged event.
        _payload = (JObject)payload.DeepClone();
    }

    // Requires a started game: LoadEventLog demands GameStarted first, so a
    // ruleset event ahead of it would produce a log that can't be replayed.
    public bool CanExecute(GameState state) => state.IsStarted;

    public IReadOnlyList<GameEvent> Execute(GameState state)
    {
        return new GameEvent[]
        {
            new RulesetEvent
            {
                Kind = _kind,
                Payload = (JObject)_payload.DeepClone(),
            }
        };
    }
}
