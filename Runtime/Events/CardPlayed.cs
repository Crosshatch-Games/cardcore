namespace CardCore.Events;

public sealed record CardPlayed : GameEvent
{
    public int PlayerId { get; init; }
    public int CardId { get; init; }
    public int HandIndexBefore { get; init; }
    public int PlayAreaIndexAfter { get; init; }
}
