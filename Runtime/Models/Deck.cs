using System;
using System.Collections.Generic;

namespace CardCore;

public sealed class Deck
{
    private readonly List<CardInstance> _cards;

    public Deck(IReadOnlyList<CardInstance> cards, Random rng)
    {
        if (cards is null) throw new ArgumentNullException(nameof(cards));
        if (rng is null) throw new ArgumentNullException(nameof(rng));
        _cards = new List<CardInstance>(cards);
        Shuffle(_cards, rng);
    }

    [Newtonsoft.Json.JsonConstructor]
    internal Deck(IReadOnlyList<CardInstance> cards)
    {
        if (cards is null) throw new ArgumentNullException(nameof(cards));
        _cards = new List<CardInstance>(cards);
    }

    public int Count => _cards.Count;

    public IReadOnlyList<CardInstance> Cards => _cards.AsReadOnly();

    public CardInstance this[int index] => _cards[index];

    public DeckRemoveResult RemoveTop()
    {
        if (_cards.Count == 0)
            throw new InvalidOperationException("Cannot remove from an empty deck.");
        var card = _cards[0];
        _cards.RemoveAt(0);
        return new DeckRemoveResult(card, IndexBefore: 0);
    }

    public CardInstance FindByInstanceId(Guid instanceId)
    {
        foreach (var c in _cards)
            if (c.InstanceId == instanceId) return c;
        throw new InvalidOperationException($"No card with InstanceId {instanceId} in deck.");
    }

    public IReadOnlyList<CardInstance> Snapshot() => _cards.AsReadOnly();

    internal void AddRange(IReadOnlyList<CardInstance> cards)
    {
        if (cards is null) throw new ArgumentNullException(nameof(cards));
        foreach (var c in cards)
        {
            _cards.Add(c);
        }
    }

    internal void ReorderTo(IReadOnlyList<Guid> postShuffleInstanceIds)
    {
        if (postShuffleInstanceIds is null)
            throw new ArgumentNullException(nameof(postShuffleInstanceIds));
        if (postShuffleInstanceIds.Count != _cards.Count)
            throw new InvalidOperationException(
                $"Deck.ReorderTo: id list length {postShuffleInstanceIds.Count} does not match deck count {_cards.Count}.");

        var byId = new Dictionary<Guid, CardInstance>(_cards.Count);
        foreach (var c in _cards)
        {
            byId[c.InstanceId] = c;
        }

        var reordered = new List<CardInstance>(_cards.Count);
        foreach (var id in postShuffleInstanceIds)
        {
            if (!byId.Remove(id, out var card))
                throw new InvalidOperationException(
                    $"Deck.ReorderTo: id {id} is not present in the deck (or appears twice).");
            reordered.Add(card);
        }

        _cards.Clear();
        _cards.AddRange(reordered);
    }

    private static void Shuffle(List<CardInstance> list, Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}

public readonly record struct DeckRemoveResult(CardInstance Card, int IndexBefore);
