using System;

namespace CardCore.Events;

public sealed record CardDrawn : GameEvent
{
    public int PlayerId { get; init; }
    public Guid InstanceId { get; init; }
    public int DeckIndexBefore { get; init; }
}
