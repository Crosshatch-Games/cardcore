using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using CardCore.Events;

namespace CardCore;

public abstract record GameEvent
{
    public int SequenceId { get; init; }
    public long Timestamp { get; init; }

    /// <summary>
    /// JSON settings configured for CardCore: polymorphic GameEvent + non-public constructors.
    /// Use this everywhere you (de)serialize GameEvents or GameState.
    /// </summary>
    public static JsonSerializerSettings JsonSettings { get; } = new()
    {
        Converters = { new GameEventConverter() },
        ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
    };
}

public sealed class GameEventConverter : JsonConverter
{
    public override bool CanConvert(System.Type objectType) => typeof(GameEvent).IsAssignableFrom(objectType);

    public override bool CanWrite => true;
    public override bool CanRead => true;

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }
        // Use a fresh serializer without our converter to avoid infinite recursion.
        var inner = new JsonSerializer
        {
            ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
        };
        var obj = JObject.FromObject(value, inner);
        obj.AddFirst(new JProperty("$type", value.GetType().Name));
        obj.WriteTo(writer);
    }

    public override object? ReadJson(JsonReader reader, System.Type objectType, object? existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null) return null;
        var obj = JObject.Load(reader);
        var typeName = obj["$type"]?.ToString()
            ?? throw new JsonSerializationException("Missing $type discriminator on GameEvent.");
        obj.Remove("$type");

        System.Type concrete = typeName switch
        {
            nameof(GameStarted) => typeof(GameStarted),
            nameof(CardDrawn)   => typeof(CardDrawn),
            nameof(CardPlayed)  => typeof(CardPlayed),
            _ => throw new JsonSerializationException($"Unknown GameEvent subtype: {typeName}"),
        };

        var inner = new JsonSerializer
        {
            ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
        };
        return obj.ToObject(concrete, inner);
    }
}
