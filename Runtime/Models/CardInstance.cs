using System;
using System.Collections.Generic;
using CardCore.Markdown;
using Newtonsoft.Json;

namespace CardCore;

public sealed class CardInstance
{
    private readonly List<string> _types;
    private readonly List<CurrencyAmount> _costs;
    private readonly List<CurrencyAmount> _rewards;
    private readonly List<CurrencyAmount> _thresholds;
    private readonly List<Action> _actions;
    private readonly List<MarkdownText> _targets;

    public Guid InstanceId { get; }
    public string DefinitionId { get; }
    public MarkdownText Name { get; }
    public IReadOnlyList<string> Types => _types;
    public IReadOnlyList<CurrencyAmount> Costs => _costs;
    public IReadOnlyList<CurrencyAmount> Rewards => _rewards;
    public IReadOnlyList<CurrencyAmount> Thresholds => _thresholds;
    public IReadOnlyList<Action> Actions => _actions;
    public IReadOnlyList<MarkdownText> Targets => _targets;
    public string? Back { get; }
    public string? Rarity { get; }
    public MarkdownText Flavor { get; }

    [JsonConstructor]
    internal CardInstance(
        Guid instanceId,
        string definitionId,
        MarkdownText? name,
        IReadOnlyList<string>? types,
        IReadOnlyList<CurrencyAmount>? costs,
        IReadOnlyList<CurrencyAmount>? rewards,
        IReadOnlyList<CurrencyAmount>? thresholds,
        IReadOnlyList<Action>? actions,
        IReadOnlyList<MarkdownText>? targets,
        string? back,
        string? rarity,
        MarkdownText? flavor)
    {
        if (instanceId == Guid.Empty)
            throw new ArgumentException("CardInstance.InstanceId must not be Guid.Empty.", nameof(instanceId));
        if (string.IsNullOrWhiteSpace(definitionId))
            throw new ArgumentException("CardInstance.DefinitionId must be non-empty.", nameof(definitionId));

        InstanceId = instanceId;
        DefinitionId = definitionId;
        Name = name ?? MarkdownText.Empty;
        _types = types is null ? new List<string>() : new List<string>(types);
        _costs = costs is null ? new List<CurrencyAmount>() : new List<CurrencyAmount>(costs);
        _rewards = rewards is null ? new List<CurrencyAmount>() : new List<CurrencyAmount>(rewards);
        _thresholds = thresholds is null ? new List<CurrencyAmount>() : new List<CurrencyAmount>(thresholds);
        _actions = actions is null ? new List<Action>() : new List<Action>(actions);
        _targets = targets is null ? new List<MarkdownText>() : new List<MarkdownText>(targets);
        Back = back;
        Rarity = rarity;
        Flavor = flavor ?? MarkdownText.Empty;
    }

    public static CardInstance From(CardDefinition def)
    {
        if (def is null) throw new ArgumentNullException(nameof(def));
        return new CardInstance(
            instanceId: Guid.NewGuid(),
            definitionId: def.Id,
            name: def.Name,
            types: def.Types,
            costs: def.Costs,
            rewards: def.Rewards,
            thresholds: def.Thresholds,
            actions: def.Actions,
            targets: def.Targets,
            back: def.Back,
            rarity: def.Rarity,
            flavor: def.Flavor);
    }

    internal void AddAction(int index, Action action)
    {
        if (action is null) throw new ArgumentNullException(nameof(action));
        if (index < 0 || index > _actions.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        _actions.Insert(index, action);
    }

    internal void RemoveAction(int index)
    {
        if (index < 0 || index >= _actions.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        _actions.RemoveAt(index);
    }

    public void ReplaceAction(int index, Action action)
    {
        if (action is null) throw new ArgumentNullException(nameof(action));
        if (index < 0 || index >= _actions.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        _actions[index] = action;
    }

    internal void SetCost(int index, CurrencyAmount cost)
    {
        if (index < 0 || index >= _costs.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        _costs[index] = cost;
    }
}
