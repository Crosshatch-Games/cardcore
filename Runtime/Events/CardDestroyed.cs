using System;

namespace CardCore.Events;

public sealed record CardDestroyed : GameEvent
{
    public int PlayerId { get; init; }
    public Guid InstanceId { get; init; }
    public int HandIndexBefore { get; init; }
}
