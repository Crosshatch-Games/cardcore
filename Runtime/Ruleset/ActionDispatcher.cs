using System;
using System.Collections.Generic;

namespace CardCore;

public sealed class ActionDispatcher
{
    private readonly Dictionary<string, IActionHandler> _byVerb = new(StringComparer.Ordinal);

    public void Register(IActionHandler handler)
    {
        if (handler is null) throw new ArgumentNullException(nameof(handler));
        if (string.IsNullOrWhiteSpace(handler.Verb))
            throw new ArgumentException("Handler.Verb must be non-empty.", nameof(handler));
        if (_byVerb.ContainsKey(handler.Verb))
            throw new InvalidOperationException(
                $"A handler for verb '{handler.Verb}' is already registered.");
        _byVerb.Add(handler.Verb, handler);
    }

    public bool IsRegistered(string verb) => _byVerb.ContainsKey(verb);

    public IReadOnlyList<GameEvent> Dispatch(Action action, CardInstance card, GameState state)
    {
        if (action is null) throw new ArgumentNullException(nameof(action));
        if (!_byVerb.TryGetValue(action.Verb, out var handler))
            throw new InvalidOperationException(
                $"No handler registered for verb '{action.Verb}'.");
        return handler.Handle(action, card, state);
    }
}
