using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using CardCore.Events;

namespace CardCore;

public sealed class GameState
{
    private readonly List<Player> _players = new();
    private readonly List<CardInstance> _playArea = new();
    private Deck? _deck;
    private int _seed;
    private bool _isStarted;

    public GameState() { }

    [JsonConstructor]
    internal GameState(
        IReadOnlyList<Player>? players,
        IReadOnlyList<CardInstance>? playArea,
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
    public IReadOnlyList<CardInstance> PlayArea => _playArea;
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
            case CardDiscarded discarded:
                ApplyCardDiscarded(discarded);
                break;
            case CardDestroyed destroyed:
                ApplyCardDestroyed(destroyed);
                break;
            case DiscardMovedToDeck moved:
                ApplyDiscardMovedToDeck(moved);
                break;
            case DeckShuffled shuffled:
                ApplyDeckShuffled(shuffled);
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
        var json = JsonConvert.SerializeObject(this, GameEvent.JsonSettings);
        return JsonConvert.DeserializeObject<GameState>(json, GameEvent.JsonSettings)!;
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
        if (top.Card.InstanceId != evt.InstanceId)
            throw new InvalidOperationException(
                $"CardDrawn.InstanceId mismatch at SequenceId {evt.SequenceId}.");
        _players[evt.PlayerId].Hand.Add(top.Card);
    }

    private void ApplyCardPlayed(CardPlayed evt)
    {
        var hand = _players[evt.PlayerId].Hand;
        if (evt.HandIndexBefore < 0 || evt.HandIndexBefore >= hand.Count)
            throw new InvalidOperationException(
                $"CardPlayed.HandIndexBefore out of range at SequenceId {evt.SequenceId}.");
        var card = hand.RemoveAt(evt.HandIndexBefore);
        if (card.InstanceId != evt.InstanceId)
            throw new InvalidOperationException(
                $"CardPlayed.InstanceId mismatch at SequenceId {evt.SequenceId}.");
        // If the event carries a snapshot of the card's actions at play time
        // (the path used since MutateLiveCardAction shipped), rebuild the
        // instance so the snapshot — not the in-hand actions — lands in PlayArea.
        // Old logs with no snapshot keep their original behavior: the in-hand
        // CardInstance moves through unchanged.
        var played = evt.ActionsAtPlayTime is { Count: > 0 }
            ? new CardInstance(
                instanceId: card.InstanceId,
                definitionId: card.DefinitionId,
                name: card.Name,
                types: card.Types,
                costs: card.Costs,
                rewards: card.Rewards,
                thresholds: card.Thresholds,
                actions: evt.ActionsAtPlayTime,
                targets: card.Targets,
                back: card.Back,
                rarity: card.Rarity,
                flavor: card.Flavor)
            : card;
        _playArea.Add(played);
    }

    private void ApplyCardDiscarded(CardDiscarded evt)
    {
        var hand = _players[evt.PlayerId].Hand;
        if (evt.HandIndexBefore < 0 || evt.HandIndexBefore >= hand.Count)
            throw new InvalidOperationException(
                $"CardDiscarded.HandIndexBefore out of range at SequenceId {evt.SequenceId}.");
        var card = hand.RemoveAt(evt.HandIndexBefore);
        if (card.InstanceId != evt.InstanceId)
            throw new InvalidOperationException(
                $"CardDiscarded.InstanceId mismatch at SequenceId {evt.SequenceId}.");
        _players[evt.PlayerId].DiscardPile.Add(card);
    }

    private void ApplyCardDestroyed(CardDestroyed evt)
    {
        var hand = _players[evt.PlayerId].Hand;
        if (evt.HandIndexBefore < 0 || evt.HandIndexBefore >= hand.Count)
            throw new InvalidOperationException(
                $"CardDestroyed.HandIndexBefore out of range at SequenceId {evt.SequenceId}.");
        var card = hand.RemoveAt(evt.HandIndexBefore);
        if (card.InstanceId != evt.InstanceId)
            throw new InvalidOperationException(
                $"CardDestroyed.InstanceId mismatch at SequenceId {evt.SequenceId}.");
        // Card vanishes — not appended to any pile.
    }

    private void ApplyDiscardMovedToDeck(DiscardMovedToDeck evt)
    {
        if (_deck is null || _deck.Count != 0)
            throw new InvalidOperationException(
                $"DiscardMovedToDeck requires empty deck at SequenceId {evt.SequenceId}.");
        var pile = _players[evt.PlayerId].DiscardPile;
        if (pile.Count != evt.InstanceIds.Count)
            throw new InvalidOperationException(
                $"DiscardMovedToDeck length mismatch at SequenceId {evt.SequenceId}: pile has {pile.Count}, event has {evt.InstanceIds.Count}.");

        var pileSnapshot = new List<CardInstance>(pile.Count);
        for (int i = 0; i < pile.Count; i++) pileSnapshot.Add(pile[i]);

        var transfer = new List<CardInstance>(pile.Count);
        foreach (var id in evt.InstanceIds)
        {
            int found = -1;
            for (int i = 0; i < pileSnapshot.Count; i++)
            {
                if (pileSnapshot[i].InstanceId == id)
                {
                    found = i;
                    break;
                }
            }
            if (found < 0)
                throw new InvalidOperationException(
                    $"DiscardMovedToDeck id {id} not present in discard pile at SequenceId {evt.SequenceId}.");
            transfer.Add(pileSnapshot[found]);
            pileSnapshot.RemoveAt(found);
        }

        while (pile.Count > 0) pile.RemoveAt(pile.Count - 1);
        _deck.AddRange(transfer);
    }

    private void ApplyDeckShuffled(DeckShuffled evt)
    {
        if (_deck is null)
            throw new InvalidOperationException(
                $"DeckShuffled requires a deck at SequenceId {evt.SequenceId}.");
        _deck.ReorderTo(evt.PostShuffleInstanceIds);
    }
}
