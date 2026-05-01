namespace CardCore.Events;


public sealed record CardDrawn : GameEvent
{
    public int PlayerId { get; init; }
    public int CardId { get; init; }
    public int DeckIndexBefore { get; init; }
}
