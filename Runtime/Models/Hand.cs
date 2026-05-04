using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace CardCore;

public sealed class Hand
{
    private readonly List<CardInstance> _cards;

    public Hand() : this(null) { }

    [JsonConstructor]
    internal Hand(IReadOnlyList<CardInstance>? cards)
    {
        _cards = cards is null ? new List<CardInstance>() : new List<CardInstance>(cards);
    }

    public int Count => _cards.Count;

    public IReadOnlyList<CardInstance> Cards => _cards.AsReadOnly();

    public CardInstance this[int index] => _cards[index];

    public void Add(CardInstance card)
    {
        if (card is null) throw new ArgumentNullException(nameof(card));
        _cards.Add(card);
    }

    public CardInstance RemoveAt(int index)
    {
        if (index < 0 || index >= _cards.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        var card = _cards[index];
        _cards.RemoveAt(index);
        return card;
    }
}
