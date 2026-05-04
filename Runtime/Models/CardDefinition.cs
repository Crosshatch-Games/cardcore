using System;
using System.Collections.Generic;
using CardCore.Markdown;
using Newtonsoft.Json;

namespace CardCore;

public sealed record CardDefinition
{
    public string Id { get; }
    public MarkdownText Name { get; }
    public IReadOnlyList<string> Types { get; }
    public IReadOnlyList<CurrencyAmount> Costs { get; }
    public IReadOnlyList<CurrencyAmount> Rewards { get; }
    public IReadOnlyList<CurrencyAmount> Thresholds { get; }
    public IReadOnlyList<Action> Actions { get; }
    public IReadOnlyList<MarkdownText> Targets { get; }
    public string? Back { get; }
    public string? Rarity { get; }
    public MarkdownText Flavor { get; }

    [JsonConstructor]
    public CardDefinition(
        string Id,
        MarkdownText? Name = null,
        IReadOnlyList<string>? Types = null,
        IReadOnlyList<CurrencyAmount>? Costs = null,
        IReadOnlyList<CurrencyAmount>? Rewards = null,
        IReadOnlyList<CurrencyAmount>? Thresholds = null,
        IReadOnlyList<Action>? Actions = null,
        IReadOnlyList<MarkdownText>? Targets = null,
        string? Back = null,
        string? Rarity = null,
        MarkdownText? Flavor = null)
    {
        ValidateId(Id);
        this.Id = Id;
        this.Name = Name ?? MarkdownText.Empty;
        this.Types = Types ?? Array.Empty<string>();
        this.Costs = Costs ?? Array.Empty<CurrencyAmount>();
        this.Rewards = Rewards ?? Array.Empty<CurrencyAmount>();
        this.Thresholds = Thresholds ?? Array.Empty<CurrencyAmount>();
        this.Actions = Actions ?? Array.Empty<Action>();
        this.Targets = Targets ?? Array.Empty<MarkdownText>();
        this.Back = Back;
        this.Rarity = Rarity;
        this.Flavor = Flavor ?? MarkdownText.Empty;
    }

    private static void ValidateId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("CardDefinition.Id must be non-empty.", nameof(id));
        for (int i = 0; i < id.Length; i++)
        {
            char c = id[i];
            if (char.IsWhiteSpace(c))
                throw new ArgumentException(
                    $"CardDefinition.Id must not contain whitespace: '{id}'.", nameof(id));
            if (char.IsUpper(c))
                throw new ArgumentException(
                    $"CardDefinition.Id must be lowercase: '{id}'.", nameof(id));
        }
    }
}
