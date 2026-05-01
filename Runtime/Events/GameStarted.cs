using System;
using System.Collections.Generic;

namespace CardCore.Events;

public sealed record GameStarted : GameEvent
{
    public IReadOnlyList<Card> InitialDeckOrder { get; init; } = Array.Empty<Card>();
    public int PlayerCount { get; init; }
    public int Seed { get; init; }
}
