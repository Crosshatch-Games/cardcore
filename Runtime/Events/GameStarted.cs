using System;
using System.Collections.Generic;

namespace CardCore.Events;

public sealed record GameStarted : GameEvent
{
    public IReadOnlyList<CardInstance> InitialDeckOrder { get; init; } = Array.Empty<CardInstance>();
    public int PlayerCount { get; init; }
    public int Seed { get; init; }
}
