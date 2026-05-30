using System;
using System.Collections.Generic;

namespace CardCore.Events;

public sealed record DiscardMovedToDeck : GameEvent
{
    public int PlayerId { get; init; }
    public IReadOnlyList<Guid> InstanceIds { get; init; } = Array.Empty<Guid>();
}
