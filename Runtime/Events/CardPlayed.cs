using System;
using System.Collections.Generic;

namespace CardCore.Events;

public sealed record CardPlayed : GameEvent
{
    public int PlayerId { get; init; }
    public Guid InstanceId { get; init; }
    public int HandIndexBefore { get; init; }
    public int PlayAreaIndexAfter { get; init; }
    public IReadOnlyList<Action> ActionsAtPlayTime { get; init; } = Array.Empty<Action>();
}
