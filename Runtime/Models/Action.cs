using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CardCore;

public sealed record Action
{
    public string Verb { get; }
    public JObject Payload { get; }

    [JsonConstructor]
    public Action(string Verb, JObject Payload)
    {
        if (string.IsNullOrWhiteSpace(Verb))
            throw new ArgumentException("Action.Verb must be non-empty.", nameof(Verb));
        if (Payload is null)
            throw new ArgumentNullException(nameof(Payload));
        this.Verb = Verb;
        this.Payload = Payload;
    }
}
