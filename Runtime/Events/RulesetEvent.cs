using Newtonsoft.Json.Linq;

namespace CardCore.Events;

/// <summary>
/// Ruleset-defined event. The engine logs, serializes and replays it but never
/// interprets <see cref="Kind"/> or <see cref="Payload"/>; GameState.Apply is a no-op for it.
/// </summary>
public sealed record RulesetEvent : GameEvent
{
    public string Kind { get; init; } = string.Empty;
    public JObject Payload { get; init; } = new JObject();
}
