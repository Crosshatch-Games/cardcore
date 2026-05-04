using System;
using System.Collections.Generic;

namespace CardCore.Catalog;

public sealed class CardCatalog
{
    private readonly Dictionary<string, CardDefinition> _byId;
    private readonly List<string> _loadWarnings;

    public CardCatalog(IEnumerable<CardDefinition> definitions)
        : this(definitions, Array.Empty<string>()) { }

    public CardCatalog(IEnumerable<CardDefinition> definitions, IReadOnlyList<string> loadWarnings)
    {
        if (definitions is null) throw new ArgumentNullException(nameof(definitions));
        if (loadWarnings is null) throw new ArgumentNullException(nameof(loadWarnings));

        _byId = new Dictionary<string, CardDefinition>();
        foreach (var def in definitions)
        {
            if (def is null) throw new ArgumentException("Catalog cannot contain null definitions.", nameof(definitions));
            if (_byId.ContainsKey(def.Id))
                throw new ArgumentException(
                    $"Duplicate CardDefinition id '{def.Id}' in catalog.", nameof(definitions));
            _byId.Add(def.Id, def);
        }
        _loadWarnings = new List<string>(loadWarnings);
    }

    public int Count => _byId.Count;

    public IReadOnlyCollection<CardDefinition> Definitions => _byId.Values;

    public IReadOnlyList<string> LoadWarnings => _loadWarnings;

    public CardDefinition Get(string id)
    {
        if (_byId.TryGetValue(id, out var def)) return def;
        throw new KeyNotFoundException($"No CardDefinition with id '{id}'.");
    }

    public bool TryGet(string id, out CardDefinition? def)
    {
        return _byId.TryGetValue(id, out def);
    }

    public bool Contains(string id) => _byId.ContainsKey(id);
}
