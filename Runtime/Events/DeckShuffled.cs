using System;
using System.Collections.Generic;

namespace CardCore.Events;

public sealed record DeckShuffled : GameEvent
{
    public int PlayerId { get; init; }
    public IReadOnlyList<Guid> PostShuffleInstanceIds { get; init; } = Array.Empty<Guid>();
}
