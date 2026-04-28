using System.Text.Json;
using System.Text.Json.Serialization;
using CardCore.Events;

namespace CardCore;

public sealed class GameState
{
    private readonly List<Player> _players = new();
    private readonly List<Card> _playArea = new();
    private Deck? _deck;
    private int _seed;
    private bool _isStarted;

    public GameState() { }

    [JsonConstructor]
    internal GameState(
        IReadOnlyList<Player>? players,
        IReadOnlyList<Card>? playArea,
        Deck? deck,
        int seed,
        bool isStarted)
    {
        if (players is not null) _players.AddRange(players);
        if (playArea is not null) _playArea.AddRange(playArea);
        _deck = deck;
        _seed = seed;
        _isStarted = isStarted;
    }

    public IReadOnlyList<Player> Players => _players;
    public IReadOnlyList<Card> PlayArea => _playArea;
    public Deck? Deck => _deck;
    public int Seed => _seed;
    public bool IsStarted => _isStarted;

    internal void Apply(GameEvent evt)
    {
        switch (evt)
        {
            case GameStarted started:
                ApplyGameStarted(started);
                break;
            case CardDrawn drawn:
                ApplyCardDrawn(drawn);
                break;
            case CardPlayed played:
                ApplyCardPlayed(played);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unknown event {evt.GetType().Name} at SequenceId {evt.SequenceId}.");
        }
    }

    // Test-only shim; same body as internal Apply. Exists so test code can
    // exercise Apply without going through the engine.
    internal void ApplyForTest(GameEvent evt) => Apply(evt);

    internal GameState Clone()
    {
        var json = JsonSerializer.Serialize(this);
        return JsonSerializer.Deserialize<GameState>(json)!;
    }

    private void ApplyGameStarted(GameStarted evt)
    {
        if (_isStarted)
            throw new InvalidOperationException(
                $"GameStarted already applied; rejected at SequenceId {evt.SequenceId}.");
        _seed = evt.Seed;
        for (int i = 0; i < evt.PlayerCount; i++)
            _players.Add(new Player(i));
        _deck = new Deck(evt.InitialDeckOrder);
        _isStarted = true;
    }

    private void ApplyCardDrawn(CardDrawn evt)
    {
        if (_deck is null || _deck.Count == 0)
            throw new InvalidOperationException(
                $"CardDrawn against empty deck at SequenceId {evt.SequenceId}.");
        var top = _deck.RemoveTop();
        if (top.Card.Id != evt.CardId)
            throw new InvalidOperationException(
                $"CardDrawn.CardId mismatch at SequenceId {evt.SequenceId}.");
        _players[evt.PlayerId].Hand.Add(top.Card);
    }

    private void ApplyCardPlayed(CardPlayed evt)
    {
        var hand = _players[evt.PlayerId].Hand;
        if (evt.HandIndexBefore < 0 || evt.HandIndexBefore >= hand.Count)
            throw new InvalidOperationException(
                $"CardPlayed.HandIndexBefore out of range at SequenceId {evt.SequenceId}.");
        var card = hand.RemoveAt(evt.HandIndexBefore);
        if (card.Id != evt.CardId)
            throw new InvalidOperationException(
                $"CardPlayed.CardId mismatch at SequenceId {evt.SequenceId}.");
        _playArea.Add(card);
    }
}
