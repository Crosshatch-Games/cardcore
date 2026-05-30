# CardCore C.3 Engine Prereq — Design

**Status:** Draft, 2026-05-30
**Scope:** CardCore engine repo. Unblocks the Unity-side C.3 plan (`docs~/2026-05-30-c3-skip-turn-counter-deck-cycle.md`) by shipping the four commands, four events, peek accessors, and per-player discard pile that plan requires.

## Goal

The Unity client's C.3 slice ("Skip Turn, Turn Counter, Real Deck Cycle") needs engine primitives for: discarding a card from hand, destroying a card from hand, moving a player's discard pile back into the shared deck, and reshuffling the deck. The client also needs cheap peek accessors for deck/discard counts to drive its `TryDraw` reshuffle loop. This spec defines those primitives.

The engine remains mechanism-only. It exposes *how* to discard, destroy, transfer, and shuffle — it has no opinion about *when*. Reshuffle policy and end-of-turn flow live in the client.

## Non-goals

- Per-player decks. The shared `GameState.Deck` becomes "player 0's deck" implicitly. Commands take `playerId` to future-proof the API; the multi-player split is a separate slice.
- End-of-turn / skip-fate logic. That's client-side (`GameRules.EndTurn`).
- Per-player turn counter / `OnTurnEnded` / `OnDrawFailed` events. Those are client-side C# events, not engine `GameEvent`s.
- Discard-from-play-area, discard-from-deck, draw-multiple-as-one-event. C.3 needs hand-only discard/destroy.
- Generic metadata bag on `CardDefinition` (`skipFate` and similar fields are client metadata, parsed client-side from the raw JSON).
- Per-player RNG seed threading. Reshuffle determinism comes from the event payload (post-shuffle id list), not from a stored seed.

## Architecture

Purely additive. No existing types renamed, no existing events changed, no existing commands modified, no existing apply paths altered. Existing event logs replay unchanged.

- **One new model**: `DiscardPile` (parallel to `Hand`).
- **One existing model gains a field**: `Player.DiscardPile`.
- **Two existing models gain internal methods**: `Deck.AddRange`, `Deck.ReorderTo`.
- **Two new public methods on `IGameEngine`**: `GetDeckCount(int playerId)`, `GetDiscardCount(int playerId)`.
- **Four new commands**: `DiscardCommand`, `DestroyCardCommand`, `MoveDiscardToDeckCommand`, `ShuffleDeckCommand`.
- **Four new events**: `CardDiscarded`, `CardDestroyed`, `DiscardMovedToDeck`, `DeckShuffled`.
- **Four new `Apply` switch cases** in `GameState`.
- **Four new entries** in the polymorphic JSON converter (`GameEventConverter`).
- **`Documentation~/unity-client.md` updated** in the same change, per the project rule.

## Components

### New: `DiscardPile` (`Runtime/Models/DiscardPile.cs`)

A new sealed class. Shape mirrors `Hand`:

- `int Count { get; }`
- `IReadOnlyList<CardInstance> Cards { get; }` (read-only view)
- `CardInstance this[int index] { get; }`
- `void Add(CardInstance card)` — null check, append.
- `CardInstance RemoveAt(int index)` — range check, remove, return.
- `void AddRange(IReadOnlyList<CardInstance> cards)` — null check, append in order. Used by `ApplyDiscardMovedToDeck` (and by future ruleset code).
- Default ctor (empty).
- `[JsonConstructor] internal DiscardPile(IReadOnlyList<CardInstance>? cards)` — null-tolerant for legacy logs.

Not a subclass of `Hand`. The shape happens to match today; future divergence (peek-top, search-by-id, "exile zone" variants) shouldn't be forced into `Hand`'s contract.

### Modified: `Player` (`Runtime/Models/Player.cs`)

Gains `DiscardPile DiscardPile { get; }`. The default ctor (`Player(int id)`) initializes both `Hand` and `DiscardPile` empty. The `[JsonConstructor]` ctor accepts an optional `DiscardPile?` (null → fresh empty pile), so saved logs from before C.3 rehydrate without a backfill step.

### Modified: `Deck` (`Runtime/Models/Deck.cs`)

Two new `internal` methods (callable only by `GameState.Apply`):

- `internal void AddRange(IReadOnlyList<CardInstance> cards)` — null check, append in order. Used during `ApplyDiscardMovedToDeck` after asserting the deck is empty.
- `internal void ReorderTo(IReadOnlyList<Guid> postShuffleInstanceIds)` — validates the supplied id set exactly matches the current deck contents (same length, same set), then rearranges the underlying list to that order. Throws `InvalidOperationException` on mismatch.

Both methods are `internal`, not `public`. The engine is the only legitimate caller; rulesets and tests reach them via the standard apply path.

### Modified: `GameState` (`Runtime/GameState.cs`)

`Apply`'s switch grows four cases:

```csharp
case CardDiscarded discarded:    ApplyCardDiscarded(discarded);    break;
case CardDestroyed destroyed:    ApplyCardDestroyed(destroyed);    break;
case DiscardMovedToDeck moved:   ApplyDiscardMovedToDeck(moved);   break;
case DeckShuffled shuffled:      ApplyDeckShuffled(shuffled);      break;
```

Each apply method is private, validates the event against the current state, and throws `InvalidOperationException` with the offending `SequenceId` on mismatch. Defensive posture matches `ApplyCardDrawn` and `ApplyCardPlayed`.

`GameState` gains **no new public properties** — the discard pile is reached via `Players[i].DiscardPile`.

### Modified: `IGameEngine` and `GameEngine`

Two new methods on both, identical signatures:

```csharp
int GetDeckCount(int playerId);
int GetDiscardCount(int playerId);
```

Behavior:

- Throw `InvalidOperationException` if `!state.IsStarted`.
- Throw `ArgumentOutOfRangeException` if `playerId < 0` or `playerId >= state.Players.Count`.
- Read directly from `_state` — no clone. That's the entire point of peek accessors: they skip the JSON round-trip that `GetCurrentState` / `GetStateAtIndex` pay.
- For now `GetDeckCount` returns `_state.Deck?.Count ?? 0` ignoring `playerId` (shared deck), but still validates `playerId` so the API contract holds when per-player decks land.

These are the only two peek accessors. `GetHand`, `GetDiscardPile`, etc. are intentionally **not** added — they'd have to return cloned collections to be safe, at which point `GetCurrentState()` is the right tool.

### Modified: `GameEventConverter` (`Runtime/GameEvent.cs`)

Four new entries in the `typeName switch` — one per new event type — so the polymorphic JSON read path can resolve them.

## Commands

All four commands follow the existing pattern: sealed class, private readonly fields, validation in the constructor, `CanExecute` is total (no throws), `Execute` returns a single-event list, no side effects.

### `DiscardCommand(int playerId, Guid instanceId)`

- **Ctor:** `playerId >= 0`, `instanceId != Guid.Empty`.
- **CanExecute:** `state.IsStarted`, `playerId` in range, card with `instanceId` exists in `state.Players[playerId].Hand`.
- **Execute:** locates the card in the hand, captures its `HandIndexBefore`, emits one `CardDiscarded`.

### `DestroyCardCommand(int playerId, Guid instanceId)`

Identical shape to `DiscardCommand`. Differs only in the event emitted (`CardDestroyed`) and the resulting state mutation (card vanishes — not transferred to the discard pile).

### `MoveDiscardToDeckCommand(int playerId)`

- **Ctor:** `playerId >= 0`.
- **CanExecute:** `state.IsStarted`, `playerId` in range, `state.Deck != null && state.Deck.Count == 0`, `state.Players[playerId].DiscardPile.Count > 0`. The deck-empty precondition is enforced by the engine so direct callers can't bypass the policy.
- **Execute:** reads the discard pile in its current order, projects each card to its `InstanceId`, emits one `DiscardMovedToDeck` carrying that `Guid` list.

### `ShuffleDeckCommand(int playerId)`

- **Ctor:** `playerId >= 0`.
- **CanExecute:** `state.IsStarted`, `playerId` in range, `state.Deck != null && state.Deck.Count > 0`. Shuffling an empty deck is rejected — it's a no-op error, not a silent skip.
- **Execute:** builds a fresh `System.Random()` (unseeded — the **event** is the deterministic record, not the RNG), Fisher-Yates shuffles a snapshot of the current deck's `InstanceId`s, emits one `DeckShuffled` carrying the post-shuffle id list.

## Events

All sealed records derived from `GameEvent`, all with `init` properties. Empty default initializers on collection-typed fields so partial deserialization doesn't leave them `null`.

```csharp
namespace CardCore.Events;

public sealed record CardDiscarded : GameEvent
{
    public int PlayerId { get; init; }
    public Guid InstanceId { get; init; }
    public int HandIndexBefore { get; init; }
}

public sealed record CardDestroyed : GameEvent
{
    public int PlayerId { get; init; }
    public Guid InstanceId { get; init; }
    public int HandIndexBefore { get; init; }
}

public sealed record DiscardMovedToDeck : GameEvent
{
    public int PlayerId { get; init; }
    public IReadOnlyList<Guid> InstanceIds { get; init; } = Array.Empty<Guid>();
}

public sealed record DeckShuffled : GameEvent
{
    public int PlayerId { get; init; }
    public IReadOnlyList<Guid> PostShuffleInstanceIds { get; init; } = Array.Empty<Guid>();
}
```

`DiscardMovedToDeck` and `DeckShuffled` carry only `Guid`s, not full `CardInstance`s. The cards already exist on `Player.DiscardPile` (or `Deck`) at replay time — the event only needs to identify *which* cards moved and *in what order*. Smaller event payloads, no duplicate state.

## Apply paths

Each new apply method is private on `GameState`, follows the existing defensive style, and throws `InvalidOperationException` with the offending `SequenceId` on any mismatch.

- **`ApplyCardDiscarded`** — asserts `HandIndexBefore` in range, removes that card, asserts removed card's `InstanceId` matches the event, appends to `Players[PlayerId].DiscardPile`.
- **`ApplyCardDestroyed`** — same as above except the removed card is dropped instead of appended anywhere.
- **`ApplyDiscardMovedToDeck`** — asserts deck is empty, asserts the supplied `InstanceIds` exactly match the discard pile (length + set equality), drains the pile in order into the deck via `Deck.AddRange`, leaves the pile empty.
- **`ApplyDeckShuffled`** — calls `Deck.ReorderTo(PostShuffleInstanceIds)`; that method does the id-set validation and rearranges in-place. If the id set doesn't match, the apply throws.

## Peek accessors

Implementation is trivial — both methods are 3-4 lines on `GameEngine`. They live on the public interface because they're the hot path for the client's `TryDraw` reshuffle loop, which checks both counts up to three times per attempted draw. Avoiding the JSON-roundtrip clone (which `GetCurrentState` pays) is the entire reason these exist.

`playerId` validation is enforced even though current behavior is shared-deck (`GetDeckCount` ignores `playerId` internally) so that the API contract is correct now and survives the future per-player-deck split.

## JSON polymorphism

Each new event gets one entry in `GameEventConverter`'s `typeName switch`:

```csharp
nameof(CardDiscarded)     => typeof(CardDiscarded),
nameof(CardDestroyed)     => typeof(CardDestroyed),
nameof(DiscardMovedToDeck) => typeof(DiscardMovedToDeck),
nameof(DeckShuffled)      => typeof(DeckShuffled),
```

Existing settings (`JsonSettings`, `ConstructorHandling.AllowNonPublicDefaultConstructor`) are reused unchanged.

## Backward compatibility

Existing event logs (pre-C.3) are unaffected:

- `Player`'s JSON constructor takes a nullable `DiscardPile?`. Missing-from-JSON → fresh empty pile.
- No existing event types are modified.
- No existing command shape changes.
- The new events don't appear in any log written before this change, so the converter's new cases are never exercised on old logs.

Logs written after C.3 lands cannot be loaded by pre-C.3 engines. That's expected and matches CardCore's policy (engine versions are pinned by Git SHA in the client; downgrade is not supported).

## Testing

Tests live in `Tests~/Runtime/`, xUnit, pure C#, mirroring the existing pattern.

- **`DiscardPileTests.cs`** — model unit tests: Add, RemoveAt range, AddRange order, JSON round-trip empty + populated.
- **`DiscardCommandTests.cs`** — CanExecute matrix (game not started / bad playerId / instanceId not in hand / happy path), Execute emits one `CardDiscarded` with correct `HandIndexBefore`.
- **`DestroyCardCommandTests.cs`** — same matrix as `DiscardCommand`, emitting `CardDestroyed`.
- **`MoveDiscardToDeckCommandTests.cs`** — CanExecute matrix (deck not empty rejects, empty discard rejects, not started rejects, happy path), Execute snapshot order preserved.
- **`ShuffleDeckCommandTests.cs`** — CanExecute matrix, Execute emits an event whose `PostShuffleInstanceIds` set equals the deck contents, multiple shuffles produce different orderings across 5 runs (probabilistic — assert "not all 5 identical").
- **`GameStateApplyTests.cs`** (new file or extended) — one test per new apply path, plus the corruption-throws cases: `ApplyDiscardMovedToDeck` rejects an id-set mismatch; `ApplyDeckShuffled` rejects length mismatch; `ApplyCardDiscarded`/`ApplyCardDestroyed` reject `InstanceId` mismatch at `HandIndexBefore`.
- **`PeekAccessorsTests.cs`** — `GetDeckCount` and `GetDiscardCount` correctness across lifecycle (pre-start throws, post-start returns 0, after draws/discards return updated counts), both throw on bad `playerId`.
- **`ReshuffleRoundTripTests.cs`** — end-to-end scenario: start game → draw all cards → discard some → move-to-deck → shuffle → draw again. Then save event log via `JsonSettings`, load into a fresh engine, assert final states are byte-identical (JSON compare).
- **`GameEventConverterTests.cs`** (extended) — each of the four new events round-trips through `JsonSettings`.

Probabilistic shuffle test uses a fixed `[Fact]` (not `[Theory]`) with 5 trial shuffles, fails only if all 5 are byte-identical to the input. Vanishing false-positive rate for an 18-card deck.

## Public API surface delta

Per project rule, `Documentation~/unity-client.md` is updated in the same change:

- **API surface section** — add `IGameEngine.GetDeckCount(int)` and `GetDiscardCount(int)` with semantics and throw contract.
- **Commands section** — add `DiscardCommand`, `DestroyCardCommand`, `MoveDiscardToDeckCommand`, `ShuffleDeckCommand` with ctor signatures and CanExecute rules.
- **Events section** — add the four new event records with field shapes.
- **Models section** — add `Player.DiscardPile` and the new `DiscardPile` class.
- **Calling conventions** — add a short note: reshuffle is client-orchestrated as two commands (`MoveDiscardToDeckCommand` then `ShuffleDeckCommand`). The engine refuses to draw from an empty deck — that's the trigger for the client's reshuffle policy.

## File deltas

### Created (production)
- `Runtime/Models/DiscardPile.cs`
- `Runtime/Commands/DiscardCommand.cs`
- `Runtime/Commands/DestroyCardCommand.cs`
- `Runtime/Commands/MoveDiscardToDeckCommand.cs`
- `Runtime/Commands/ShuffleDeckCommand.cs`
- `Runtime/Events/CardDiscarded.cs`
- `Runtime/Events/CardDestroyed.cs`
- `Runtime/Events/DiscardMovedToDeck.cs`
- `Runtime/Events/DeckShuffled.cs`

### Created (tests)
- `Tests~/Runtime/DiscardPileTests.cs`
- `Tests~/Runtime/DiscardCommandTests.cs`
- `Tests~/Runtime/DestroyCardCommandTests.cs`
- `Tests~/Runtime/MoveDiscardToDeckCommandTests.cs`
- `Tests~/Runtime/ShuffleDeckCommandTests.cs`
- `Tests~/Runtime/PeekAccessorsTests.cs`
- `Tests~/Runtime/ReshuffleRoundTripTests.cs`

### Modified
- `Runtime/IGameEngine.cs` — adds two methods.
- `Runtime/GameEngine.cs` — implements two methods.
- `Runtime/GameState.cs` — adds 4 switch cases + 4 private apply methods.
- `Runtime/GameEvent.cs` — adds 4 entries to converter switch.
- `Runtime/Models/Player.cs` — adds `DiscardPile` property + JSON ctor null tolerance.
- `Runtime/Models/Deck.cs` — adds `AddRange` and `ReorderTo` internal methods.
- `Tests~/Runtime/GameStateApplyTests.cs` — extended (or created if absent) with 4 new apply paths + corruption-throws cases.
- `Tests~/Runtime/GameEventConverterTests.cs` — extended with 4 new event round-trip cases.
- `Documentation~/unity-client.md` — additive API surface updates.

## Out of scope

- Engine-side reshuffle policy (stays in client `GameRules.TryDraw`).
- Per-player deck split.
- Discard-from-play-area or discard-from-deck commands.
- Card metadata bag on `CardDefinition`.
- Win/lose condition events (`OnDrawFailed` is a client-side C# event, not a `GameEvent`).
- Turn counter on engine side (client-side state).
- `Hand → DiscardPile` migration of existing data (no existing logs have discard piles).

## Open question

None blocking. The plan elsewhere flags `ColliderButton` Pattern A vs B as an open scene-side question — that's purely Unity client; this engine spec is unaffected.
