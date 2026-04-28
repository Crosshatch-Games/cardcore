using System.Text.Json.Serialization;

namespace CardCore;

public sealed class Hand
{
    private readonly List<Card> _cards;

    public Hand() : this(null) { }

    [JsonConstructor]
    internal Hand(IReadOnlyList<Card>? cards)
    {
        _cards = cards is null ? new List<Card>() : new List<Card>(cards);
    }

    public int Count => _cards.Count;

    public IReadOnlyList<Card> Cards => _cards.AsReadOnly();

    public Card this[int index] => _cards[index];

    public void Add(Card card)
    {
        if (card is null) throw new ArgumentNullException(nameof(card));
        _cards.Add(card);
    }

    public Card RemoveAt(int index)
    {
        if (index < 0 || index >= _cards.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        var card = _cards[index];
        _cards.RemoveAt(index);
        return card;
    }
}
