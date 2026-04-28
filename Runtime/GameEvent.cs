using System.Text.Json.Serialization;
using CardCore.Events;

namespace CardCore;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(GameStarted), "GameStarted")]
[JsonDerivedType(typeof(CardDrawn), "CardDrawn")]
[JsonDerivedType(typeof(CardPlayed), "CardPlayed")]
public abstract record GameEvent
{
    public int SequenceId { get; init; }
    public long Timestamp { get; init; }
}
