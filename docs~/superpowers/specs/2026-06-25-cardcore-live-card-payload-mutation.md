# CardCore spec: Live-card payload mutation for play-time decisions

**Date:** 2026-06-25
**Author:** Dave Cross (Hearts and Hooves client)
**Audience:** CardCore engine maintainer
**Status:** Proposed

---

## Summary

CardCore clients need to bake play-time decisions (placement positions, target zones, branch choices, RNG values) into a `CardInstance.Action.Payload` **before** the card is played. The mutation must stick on the live engine state so that replay (`engine.GetStateAtIndex(n)`) reproduces the same decisions deterministically.

The current public surface (`GetCurrentState()` returns a clone; `CardInstance.ReplaceAction` is public but operates on whatever instance you hold) makes the obvious code path silently wrong: mutate the clone, see the live engine ignore your change, observe success forward but failure on replay.

This spec proposes a small addition to `IGameEngine` that exposes the live `CardInstance` for mutation, gated to before-commit only, with engine-side validation.

---

## Why this matters now (concrete example from Hearts and Hooves)

The deck-building prototype's `ConfirmPlay` looks like this today:

```csharp
public void ConfirmPlay(int handIndex, Vector3? placementPosition = null)
{
    var state = _engine.GetCurrentState();           // <-- CLONE
    var card  = state.Players[0].Hand.Cards[handIndex];

    if (placementPosition.HasValue)
    {
        SpawnPiecePayload.Freeze(card, placementPosition.Value);
        // SpawnPiecePayload.Freeze internally calls card.ReplaceAction(0, ...)
        // but `card` is from a clone, so the live engine's CardInstance still
        // has the original empty payload.
    }

    foreach (var action in card.Actions)
        _dispatcher.Dispatch(action, card, state);  // OK — dispatch reads the clone

    _engine.ExecuteCommand(new PlayCardCommand(playerId: 0, handIndex: handIndex));
    _engine.ExecuteCommand(new DrawCardCommand(playerId: 0));
}
```

**Forward play works** because the dispatch loop reads from the same clone that got mutated.

**Replay breaks** because the engine's live `CardInstance` (the one that `PlayCardCommand` moves into `PlayArea`) never received the `ReplaceAction` call. When the scrubber later rebuilds via `engine.GetStateAtIndex(n)`, the card in the rebuilt state has an empty payload, and `SpawnPieceHandler.Handle` throws:

```
InvalidOperationException: spawn_piece action on card 'cottage' has no position payload — ConfirmPlay must call ReplaceAction before dispatch.
```

This is not a bug in the handler. The handler is reading from `action.Payload`, exactly as the scrub-safe payload pattern requires (`project_scrub_safe_payload_pattern.md`). The problem is **there is no engine API to mutate the payload on the live card**.

---

## Constraints

The CardCore engine has rules that this spec must respect:

- `GameState` and its members are exposed read-only via clones (`GameEngine.GetCurrentState() => _state.Clone()`).
- `Apply` is internal — only the engine mutates state.
- The event log is the source of truth; per-event invariants (SequenceId contiguity, single GameStarted, …) are enforced in `LoadEventLog`.
- All fields private; all classes JSON-serializable; default `sealed`.
- `CardInstance.ReplaceAction(int index, Action action)` is **already public** — the missing piece is a way to get to the *live* `CardInstance`, not a missing API on `CardInstance` itself.

The spec must NOT:
- Break encapsulation by exposing `GameState` directly.
- Allow arbitrary state mutation outside the command/event flow.
- Make replay non-deterministic.

---

## Proposed change: `IGameEngine.MutateLiveCardAction`

Add one method to `IGameEngine`:

```csharp
namespace CardCore;

public interface IGameEngine
{
    // ... existing surface ...

    /// <summary>
    /// Replace an action on the live CardInstance identified by instanceId,
    /// in-place, before the card is played. The mutation persists on the
    /// engine's live state, so replay via GetStateAtIndex reproduces the
    /// mutated payload.
    ///
    /// Validity rules (engine enforces; throws InvalidOperationException on violation):
    ///   1. The card identified by instanceId MUST currently reside in a player's
    ///      Hand. Cards in PlayArea, Deck, or DiscardPile are immutable — once a
    ///      card has been played, its actions are frozen in event-log history.
    ///   2. actionIndex MUST be in range [0, card.Actions.Count).
    ///   3. action MUST NOT be null.
    ///
    /// Threading: not thread-safe (same as the rest of IGameEngine).
    ///
    /// Replay semantics: this mutation is NOT recorded as an event. It edits the
    /// engine's working CardInstance directly. Because CardInstance carries its
    /// own state through GameStarted's InitialDeckOrder payload, the mutation
    /// survives JSON round-trip via the existing serialization path.
    /// </summary>
    void MutateLiveCardAction(Guid instanceId, int actionIndex, Action action);
}
```

### Implementation sketch

`GameEngine` already has access to `_state` (private). `_state.Players[p].Hand.Cards` exposes `IReadOnlyList<CardInstance>` — but `CardInstance.ReplaceAction` is public, so the engine can call it on whichever live instance it finds:

```csharp
public void MutateLiveCardAction(Guid instanceId, int actionIndex, Action action)
{
    if (action is null) throw new ArgumentNullException(nameof(action));

    foreach (var player in _state.Players)
    {
        foreach (var card in player.Hand.Cards)
        {
            if (card.InstanceId == instanceId)
            {
                card.ReplaceAction(actionIndex, action);
                return;
            }
        }
    }

    throw new InvalidOperationException(
        $"MutateLiveCardAction: card with InstanceId {instanceId} is not in any player's hand. " +
        "Only cards currently in hand can be mutated; cards in PlayArea/Deck/DiscardPile are immutable.");
}
```

(Linear scan is fine — hand sizes are small. Optimize later if needed.)

### Why "in hand only"?

- Cards in **PlayArea**, **DiscardPile**, **Deck** represent historical or future state. Mutating them would corrupt the event log's invariant that "state at event N is fully determined by events 0..N." The mutated payload would not have a corresponding event.
- Cards in **Hand** have not yet been the subject of a `CardPlayed` event. Mutating them before `PlayCardCommand` runs is functionally equivalent to authoring the card with the right payload to begin with — except the value comes from runtime (player input) instead of static authoring.

### Why this preserves replay determinism

- `GameStarted` carries `InitialDeckOrder`, which is `IReadOnlyList<CardInstance>` and includes each card's actions.
- During `LoadEventLog`, the engine replays `GameStarted` and reconstructs the deck — but at this point the cards have their **original** actions (not the live-mutated ones).
- **Critical:** for replay determinism, `GameStarted` would need to either:
  - **(a)** Capture the cards' actions at `StartGameCommand` time (current behavior) and re-mutate during replay — but that requires re-firing the mutations, which aren't logged.
  - **(b)** Or the mutation must be **logged as part of `CardPlayed`** so replay sees the final action set.

**(b) is the correct answer.** See "Required companion change" below.

---

## Required companion change: `CardPlayed` carries the mutated action snapshot

Today's `CardPlayed`:

```csharp
public sealed record CardPlayed(...) : GameEvent
{
    public Guid InstanceId { get; init; }
    public int PlayAreaIndexAfter { get; init; }
    // ... player id, hand index before, etc.
}
```

The event tells the engine "this card was played" but does NOT capture the card's action payloads at play time. So during replay, `GameState.Apply(CardPlayed)` reads the card from the hand (with original empty payload) and moves it to `PlayArea`. The mutation made via `MutateLiveCardAction` is lost.

**Proposed:** add the played card's action snapshot to `CardPlayed`:

```csharp
public sealed record CardPlayed(...) : GameEvent
{
    public Guid InstanceId { get; init; }
    public int PlayAreaIndexAfter { get; init; }
    public IReadOnlyList<Action> ActionsAtPlayTime { get; init; }  // NEW
    // ...
}
```

`GameState.Apply(CardPlayed)`:
1. Remove the `CardInstance` from the player's hand by `InstanceId`.
2. **Reconstruct it with `ActionsAtPlayTime` as its actions list** (preserving InstanceId/DefinitionId/everything else).
3. Add to `PlayArea`.

This way, replay sees the same payload that the original `MutateLiveCardAction` baked in — without needing to log the mutation itself as a separate event.

Backwards compatibility: old event logs without `ActionsAtPlayTime` can be handled by falling back to the in-hand card's actions (the current behavior). New plays always populate the field.

### Alternative considered

**Log a `CardActionMutated` event** every time `MutateLiveCardAction` is called. Simpler engine-side; but it bloats the event log with N+1 events per play (one mutation per action mutated, plus the play). The `ActionsAtPlayTime` snapshot is more compact and ties the mutation to the play it serves.

---

## Client-side usage after this lands

`ConfirmPlay` becomes:

```csharp
public void ConfirmPlay(int handIndex, Vector3? placementPosition = null)
{
    OnBeforeCommand?.Invoke();
    var state = _engine.GetCurrentState();
    var card  = state.Players[0].Hand.Cards[handIndex];

    if (placementPosition.HasValue)
    {
        // Build the mutated action and push to the LIVE card via the engine.
        int spawnIdx = FindSpawnPieceActionIndex(card.Actions);
        var mutated  = SpawnPiecePayload.Build(card.Actions[spawnIdx], placementPosition.Value);
        _engine.MutateLiveCardAction(card.InstanceId, spawnIdx, mutated);

        // Repeat for target_zone_id on Tools cards.
        int zoneIdx = FindEnterPathDrawingActionIndex(card.Actions);
        if (zoneIdx >= 0)
        {
            string targetZoneId = FindZoneAtPosition(placementPosition.Value);
            if (targetZoneId != null)
            {
                var withZone = EnterPathDrawingPayload.Build(card.Actions[zoneIdx], targetZoneId);
                _engine.MutateLiveCardAction(card.InstanceId, zoneIdx, withZone);
            }
        }
    }

    // Now the LIVE card has the mutated actions.
    // Re-fetch live state so dispatch reads the same payload that's in the engine.
    var liveState = _engine.GetCurrentState();
    var liveCard  = liveState.Players[0].Hand.Cards[handIndex];
    foreach (var action in liveCard.Actions)
        _dispatcher.Dispatch(action, liveCard, liveState);

    _engine.ExecuteCommand(new PlayCardCommand(playerId: 0, handIndex: handIndex));
    _engine.ExecuteCommand(new DrawCardCommand(playerId: 0));
}
```

`SpawnPiecePayload.Freeze` and `EnterPathDrawingPayload.Freeze` (client-side helpers) are reworked to **return** the mutated action rather than mutate in-place via a clone. The mutation happens on the engine via `MutateLiveCardAction`.

---

## Tests CardCore should add

1. **`MutateLiveCardAction_OnHandCard_PersistsAcrossClone`**
   Push a card into a hand, call `MutateLiveCardAction`, call `GetCurrentState()`, assert the clone's card has the mutated action.

2. **`MutateLiveCardAction_OnHandCard_PersistsAcrossPlay`**
   Push, mutate, execute `PlayCardCommand`, call `GetCurrentState()`, assert the `PlayArea` card has the mutated action.

3. **`MutateLiveCardAction_OnHandCard_PersistsAcrossRoundTrip`**
   Push, mutate, play, `GetEventLog()`, `LoadEventLog` into a fresh engine, assert the rebuilt `PlayArea` card has the mutated action.

4. **`MutateLiveCardAction_OnPlayedCard_Throws`**
   Push, play, then call `MutateLiveCardAction` on the now-in-PlayArea card → expects `InvalidOperationException`.

5. **`MutateLiveCardAction_OnDeckCard_Throws`**
   Same, on a card still in the deck (not drawn yet).

6. **`MutateLiveCardAction_NotFound_Throws`**
   Unknown InstanceId → expects `InvalidOperationException`.

7. **`MutateLiveCardAction_NullAction_Throws`**
   `action: null` → `ArgumentNullException`.

8. **`MutateLiveCardAction_IndexOutOfRange_Throws`**
   `actionIndex: -1` or `>= card.Actions.Count` → `ArgumentOutOfRangeException` (from `CardInstance.ReplaceAction`).

---

## Migration notes for existing consumers

This is a **breaking change** for any client that relies on `CardPlayed` not carrying `ActionsAtPlayTime` (none today as far as I know — the field is new). For clients not using payload mutation, behavior is unchanged: `ActionsAtPlayTime` would just snapshot the unchanged action list and replay produces identical results.

Hearts and Hooves is the only known client and explicitly needs this feature.

---

## Open questions for the CardCore maintainer

1. **`Action` mutability:** does the spec assume `Action` is a value-like record with a `with`-able `Payload`? Confirm or propose alternative shape.
2. **Snapshot location:** should `ActionsAtPlayTime` go on `CardPlayed`, or be a separate `CardActionsResolved` event fired right before `CardPlayed`? Either works; `CardPlayed` is more compact.
3. **`MutateLiveCardAction` naming:** alternatives — `ReplaceHandCardAction`, `SetActionOnHandCard`, etc. The signal we want is "this only works on cards in hand, before play."
4. **Index vs. predicate:** should the API also offer `MutateLiveCardAction(Guid, Func<Action, bool>, Action)` to find by predicate instead of position? Current sites in HoH always know the index, so position-based is sufficient.

---

## What we'll do client-side until this ships

Pin to the current CardCore SHA. Document the limitation in HoH's `project_c2b_scrub_defer.md` memory (already exists). The C.X scrubber slice will be considered "partial" — undo works for everything except Piece-card plays. Once this CardCore change lands, the HoH client adapts `ConfirmPlay`, the deferred Piece scrub-safety is fixed, and Task 23 (regression pass) can complete.
