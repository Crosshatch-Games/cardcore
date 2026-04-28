namespace CardCore;

public sealed class Deck
{
    private readonly List<Card> _cards;

    public Deck(IReadOnlyList<Card> cards, Random rng)
    {
        if (cards is null) throw new ArgumentNullException(nameof(cards));
        if (rng is null) throw new ArgumentNullException(nameof(rng));
        _cards = new List<Card>(cards);
        Shuffle(_cards, rng);
    }

    // Used by replay AND JSON deserialization (no shuffle, accepts known order).
    [System.Text.Json.Serialization.JsonConstructor]
    internal Deck(IReadOnlyList<Card> cards)
    {
        if (cards is null) throw new ArgumentNullException(nameof(cards));
        _cards = new List<Card>(cards);
    }

    public int Count => _cards.Count;

    public IReadOnlyList<Card> Cards => _cards.AsReadOnly();

    public Card this[int index] => _cards[index];

    public DeckRemoveResult RemoveTop()
    {
        if (_cards.Count == 0)
            throw new InvalidOperationException("Cannot remove from an empty deck.");
        var card = _cards[0];
        _cards.RemoveAt(0);
        return new DeckRemoveResult(card, IndexBefore: 0);
    }

    public Card FindCardById(int id)
    {
        foreach (var c in _cards)
            if (c.Id == id) return c;
        throw new InvalidOperationException($"No card with id {id} in deck.");
    }

    public IReadOnlyList<Card> Snapshot() => _cards.AsReadOnly();

    private static void Shuffle(List<Card> list, Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}

public readonly record struct DeckRemoveResult(Card Card, int IndexBefore);
