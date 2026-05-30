# CardCore C.3 Engine Prereq Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the engine primitives the Unity-side C.3 slice needs: four new commands (`DiscardCommand`, `DestroyCardCommand`, `MoveDiscardToDeckCommand`, `ShuffleDeckCommand`), four new events (`CardDiscarded`, `CardDestroyed`, `DiscardMovedToDeck`, `DeckShuffled`), one new model (`DiscardPile`) attached to `Player`, and two new peek accessors (`GetDeckCount(int)`, `GetDiscardCount(int)`) on `IGameEngine`.

**Architecture:** Purely additive. No existing types renamed, no existing commands/events modified, no existing apply paths altered. The engine remains mechanism-only — it knows how to discard, destroy, transfer, and shuffle, but has no opinion about when. Reshuffle policy stays in the Unity client. New events follow the same shape as existing ones (sealed records with `init` properties), new commands follow the existing pattern (sealed class, private readonly fields, validation in ctor, total `CanExecute`, single-event `Execute`), all four new events flow through the existing `GameState.Apply` switch and the polymorphic `GameEventConverter` switch.

**Tech Stack:** Pure C# (.NET 9.0), xUnit 2.9.2 for tests, Newtonsoft.Json for serialization, no Unity dependencies. Tests live in `Tests~/PureCSharp/` (project `CardCore.PureTests.csproj`), runtime in `Runtime/` (project `CardCore.csproj`).

**Reference spec:** `docs~/superpowers/specs/2026-05-30-c3-engine-prereq-design.md`

---

## File Structure

### Created (production, 9 files)
- `Runtime/Models/DiscardPile.cs` — new model class parallel to `Hand`.
- `Runtime/Commands/DiscardCommand.cs` — moves a card from hand to the player's discard pile.
- `Runtime/Commands/DestroyCardCommand.cs` — removes a card from hand entirely.
- `Runtime/Commands/MoveDiscardToDeckCommand.cs` — empties a player's discard pile into the shared deck.
- `Runtime/Commands/ShuffleDeckCommand.cs` — emits a `DeckShuffled` event whose payload is the post-shuffle id order.
- `Runtime/Events/CardDiscarded.cs` — `(PlayerId, InstanceId, HandIndexBefore)`.
- `Runtime/Events/CardDestroyed.cs` — same shape as `CardDiscarded`.
- `Runtime/Events/DiscardMovedToDeck.cs` — `(PlayerId, IReadOnlyList<Guid> InstanceIds)`.
- `Runtime/Events/DeckShuffled.cs` — `(PlayerId, IReadOnlyList<Guid> PostShuffleInstanceIds)`.

### Created (tests, 7 files)
- `Tests~/PureCSharp/DiscardPileTests.cs`
- `Tests~/PureCSharp/DiscardCommandTests.cs`
- `Tests~/PureCSharp/DestroyCardCommandTests.cs`
- `Tests~/PureCSharp/MoveDiscardToDeckCommandTests.cs`
- `Tests~/PureCSharp/ShuffleDeckCommandTests.cs`
- `Tests~/PureCSharp/PeekAccessorsTests.cs`
- `Tests~/PureCSharp/ReshuffleRoundTripTests.cs`

### Modified (production)
- `Runtime/IGameEngine.cs` — adds two methods to the interface.
- `Runtime/GameEngine.cs` — implements two methods on the concrete engine.
- `Runtime/GameState.cs` — adds 4 switch cases + 4 private apply methods.
- `Runtime/GameEvent.cs` — adds 4 entries to the `GameEventConverter` type switch.
- `Runtime/Models/Player.cs` — adds `DiscardPile DiscardPile { get; }` + JSON ctor null tolerance.
- `Runtime/Models/Deck.cs` — adds `internal void AddRange(...)` and `internal void ReorderTo(...)`.

### Modified (tests)
- `Tests~/PureCSharp/GameStateTests.cs` — extended with the 4 new apply paths + corruption-throws cases.
- `Tests~/PureCSharp/GameEventTests.cs` — extended with 4 new event round-trip cases.
- `Tests~/PureCSharp/PlayerTests.cs` — extended with DiscardPile property tests + legacy-JSON tolerance.
- `Tests~/PureCSharp/DeckTests.cs` — extended with AddRange + ReorderTo internal-method tests (uses `InternalsVisibleTo` already in place if present; otherwise tests call through GameState.Apply).

### Modified (docs)
- `Documentation~/unity-client.md` — public API surface delta (per project rule).

---

## Pre-flight context

- **Working directory.** `/Users/davecross/Documents/GitHub/cardcore`. Confirm with `pwd`.
- **Branch.** Work on a feature branch `feature/c3-engine-prereq`. Current HEAD is `main` at `05682c9` (matches the C.3 Unity-side plan's pre-bump SHA exactly).
- **No autonomous commits.** Project convention (memory: `feedback_no_commits.md`): every task ends with `git add` + `git status`. The user runs `git commit` themselves. Each task's "Commit" step is a **stage** step, not a commit step.
- **Test command.** `dotnet test Tests~/PureCSharp/CardCore.PureTests.csproj` from the repo root. To run a single test class: `dotnet test Tests~/PureCSharp/CardCore.PureTests.csproj --filter "FullyQualifiedName~CardCore.PureTests.DiscardCommandTests"`.
- **xUnit, not NUnit.** Existing tests use `[Fact]`, `Assert.Equal(expected, actual)`, `Assert.Throws<T>(...)`, `Assert.Single`, `Assert.IsType<T>(...)`, `Assert.True/False`, `Assert.Same/NotSame`. Match the style.
- **Test helper pattern.** Existing test classes define static helpers at the top: `NewCard(string defId = "c")` returns `CardInstance.From(new CardDefinition(defId))`; `SmallDeck()` returns `List<CardInstance>`; `StartedState(int playerCount, params CardInstance[] deck)` uses `GameState.ApplyForTest(new GameStarted { ... })`. Reuse this pattern in every new test file.
- **`GameState.ApplyForTest`** is the test-only shim that lets tests apply events directly to a state without going through the engine. It's `internal`, accessible to the test project via the existing `InternalsVisibleTo("CardCore.PureTests")` (verify in `Runtime/CardCore.csproj` if needed).
- **`Deck` ctor for tests.** Two ctors: `public Deck(IReadOnlyList<CardInstance> cards, Random rng)` (shuffles) and `[JsonConstructor] internal Deck(IReadOnlyList<CardInstance> cards)` (no shuffle). Tests use either depending on intent.
- **`Player` ctor.** Today `Player(int id)` initializes only `Hand`. The `[JsonConstructor] internal Player(int id, Hand hand)` accepts a nullable-ish `hand`. We will add a `DiscardPile` field with parallel construction.
- **`GameStarted` carries the post-shuffle deck.** Replay never re-runs RNG — `InitialDeckOrder` is the authoritative order. Our `DeckShuffled` event follows the same pattern: it carries `PostShuffleInstanceIds`, and `ApplyDeckShuffled` reorders the existing deck to match. The command itself uses `new Random()` (unseeded) — the *event* is the source of truth.
- **Snapshots are pure.** `Hand.Cards` returns `_cards.AsReadOnly()`. `Deck.Snapshot()` and `Deck.Cards` do the same. `DiscardPile` will mirror this.
- **Event field defaults.** `IReadOnlyList<Guid>` properties on new events get `= Array.Empty<Guid>()` initializers so partial deserialization doesn't leave them `null`.
- **`Documentation~/unity-client.md` rule.** Per `CLAUDE.md`: whenever public API surface changes, update this doc in the same change. This plan dedicates Task 13 to it.

---

## Task 0: Verify working directory + create feature branch

**Files:**
- None modified

- [ ] **Step 1: Confirm working directory and branch state**

```bash
pwd
git status
git log --oneline -3
```

Expected: `/Users/davecross/Documents/GitHub/cardcore`, clean working tree, HEAD at `05682c9` on `main` (or whichever SHA is current).

- [ ] **Step 2: Create the feature branch**

```bash
git checkout -b feature/c3-engine-prereq
```

Expected: `Switched to a new branch 'feature/c3-engine-prereq'`.

- [ ] **Step 3: Verify the test suite is currently green**

```bash
dotnet test Tests~/PureCSharp/CardCore.PureTests.csproj
```

Expected: all tests pass. Record the green count — every later task must keep this baseline. If anything fails, stop and resolve before continuing.

---

## Task 1: `DiscardPile` model — write tests, then implementation

**Files:**
- Create: `Tests~/PureCSharp/DiscardPileTests.cs`
- Create: `Runtime/Models/DiscardPile.cs`

`DiscardPile` mirrors `Hand` in shape. New class (not a `Hand` subclass) so future divergence (peek-top, search-by-id) doesn't get forced into `Hand`'s contract.

- [ ] **Step 1: Write the failing tests**

Create `Tests~/PureCSharp/DiscardPileTests.cs` with EXACTLY this content:

```csharp
using System;
using System.Collections.Generic;
using CardCore;
using Newtonsoft.Json;
using Xunit;

namespace CardCore.PureTests;

public class DiscardPileTests
{
    private static CardInstance NewCard(string defId = "c") =>
        CardInstance.From(new CardDefinition(defId));

    [Fact]
    public void Empty_HasZeroCount()
    {
        var pile = new DiscardPile();
        Assert.Equal(0, pile.Count);
        Assert.Empty(pile.Cards);
    }

    [Fact]
    public void Add_AppendsCard()
    {
        var pile = new DiscardPile();
        var card = NewCard("a");

        pile.Add(card);

        Assert.Equal(1, pile.Count);
        Assert.Same(card, pile[0]);
    }

    [Fact]
    public void Add_NullCard_Throws()
    {
        var pile = new DiscardPile();
        Assert.Throws<ArgumentNullException>(() => pile.Add(null!));
    }

    [Fact]
    public void RemoveAt_RemovesAndReturnsCard()
    {
        var pile = new DiscardPile();
        var a = NewCard("a");
        var b = NewCard("b");
        pile.Add(a);
        pile.Add(b);

        var removed = pile.RemoveAt(0);

        Assert.Same(a, removed);
        Assert.Equal(1, pile.Count);
        Assert.Same(b, pile[0]);
    }

    [Fact]
    public void RemoveAt_OutOfRange_Throws()
    {
        var pile = new DiscardPile();
        pile.Add(NewCard("a"));
        Assert.Throws<ArgumentOutOfRangeException>(() => pile.RemoveAt(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => pile.RemoveAt(5));
    }

    [Fact]
    public void AddRange_AppendsInOrder()
    {
        var pile = new DiscardPile();
        var a = NewCard("a");
        var b = NewCard("b");
        var c = NewCard("c");

        pile.AddRange(new List<CardInstance> { a, b, c });

        Assert.Equal(3, pile.Count);
        Assert.Same(a, pile[0]);
        Assert.Same(b, pile[1]);
        Assert.Same(c, pile[2]);
    }

    [Fact]
    public void AddRange_NullCollection_Throws()
    {
        var pile = new DiscardPile();
        Assert.Throws<ArgumentNullException>(() => pile.AddRange(null!));
    }

    [Fact]
    public void JsonRoundTrip_PreservesContents()
    {
        var pile = new DiscardPile();
        pile.Add(NewCard("a"));
        pile.Add(NewCard("b"));

        var json = JsonConvert.SerializeObject(pile, GameEvent.JsonSettings);
        var rehydrated = JsonConvert.DeserializeObject<DiscardPile>(json, GameEvent.JsonSettings)!;

        Assert.Equal(2, rehydrated.Count);
        Assert.Equal("a", rehydrated[0].DefinitionId);
        Assert.Equal("b", rehydrated[1].DefinitionId);
    }

    [Fact]
    public void JsonRoundTrip_Empty_Works()
    {
        var pile = new DiscardPile();
        var json = JsonConvert.SerializeObject(pile, GameEvent.JsonSettings);
        var rehydrated = JsonConvert.DeserializeObject<DiscardPile>(json, GameEvent.JsonSettings)!;

        Assert.Equal(0, rehydrated.Count);
    }
}
```

- [ ] **Step 2: Run tests; verify they fail**

```bash
dotnet test Tests~/PureCSharp/CardCore.PureTests.csproj --filter "FullyQualifiedName~CardCore.PureTests.DiscardPileTests"
```

Expected: compile errors — `DiscardPile` type doesn't exist.

- [ ] **Step 3: Create `Runtime/Models/DiscardPile.cs`**

Create `Runtime/Models/DiscardPile.cs` with EXACTLY this content:

```csharp
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace CardCore;

public sealed class DiscardPile
{
    private readonly List<CardInstance> _cards;

    public DiscardPile() : this(null) { }

    [JsonConstructor]
    internal DiscardPile(IReadOnlyList<CardInstance>? cards)
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

    public void AddRange(IReadOnlyList<CardInstance> cards)
    {
        if (cards is null) throw new ArgumentNullException(nameof(cards));
        foreach (var c in cards)
        {
            if (c is null) throw new ArgumentException("DiscardPile.AddRange: cards must not contain nulls.", nameof(cards));
            _cards.Add(c);
        }
    }
}
```

- [ ] **Step 4: Run tests; verify they pass**

```bash
dotnet test Tests~/PureCSharp/CardCore.PureTests.csproj --filter "FullyQualifiedName~CardCore.PureTests.DiscardPileTests"
```

Expected: all 9 pass.

- [ ] **Step 5: Stage**

```bash
git add Runtime/Models/DiscardPile.cs Tests~/PureCSharp/DiscardPileTests.cs
git status
```

Stop and await user commit.

---

## Task 2: `Player.DiscardPile` — extend Player + tests

**Files:**
- Modify: `Runtime/Models/Player.cs`
- Modify: `Tests~/PureCSharp/PlayerTests.cs`

`Player` gains `DiscardPile DiscardPile { get; }`. Default ctor initializes empty. JSON ctor accepts an optional pile (null → empty), so pre-C.3 saved logs rehydrate cleanly.

- [ ] **Step 1: Read the current `PlayerTests.cs` to preserve its style**

```bash
cat Tests~/PureCSharp/PlayerTests.cs
```

Note the existing test class structure (xUnit `[Fact]` methods, no helper constructors needed for `Player`). New tests will be appended at the end of the class.

- [ ] **Step 2: Append failing tests to `PlayerTests.cs`**

Open `Tests~/PureCSharp/PlayerTests.cs`. Add these test methods inside the existing `PlayerTests` class (before its closing brace):

```csharp
    [Fact]
    public void DefaultCtor_InitializesEmptyDiscardPile()
    {
        var player = new Player(0);
        Assert.NotNull(player.DiscardPile);
        Assert.Equal(0, player.DiscardPile.Count);
    }

    [Fact]
    public void JsonCtor_NullDiscardPile_InitializesEmpty()
    {
        // Simulates a legacy event log written before DiscardPile existed:
        // the JSON has no "DiscardPile" field, so it deserializes as null.
        var json = "{\"Id\":0,\"Hand\":{\"Cards\":[]}}";
        var player = Newtonsoft.Json.JsonConvert.DeserializeObject<Player>(
            json, CardCore.GameEvent.JsonSettings)!;

        Assert.NotNull(player.DiscardPile);
        Assert.Equal(0, player.DiscardPile.Count);
    }

    [Fact]
    public void JsonRoundTrip_PreservesDiscardPileContents()
    {
        var card = CardInstance.From(new CardDefinition("d"));
        var pile = new DiscardPile();
        pile.Add(card);
        var player = new Player(0, new Hand(), pile);

        var json = Newtonsoft.Json.JsonConvert.SerializeObject(player, CardCore.GameEvent.JsonSettings);
        var rehydrated = Newtonsoft.Json.JsonConvert.DeserializeObject<Player>(
            json, CardCore.GameEvent.JsonSettings)!;

        Assert.Equal(1, rehydrated.DiscardPile.Count);
        Assert.Equal("d", rehydrated.DiscardPile[0].DefinitionId);
    }
```

(The third test references `new Player(0, new Hand(), pile)` — a 3-arg internal ctor that will exist after Step 4.)

- [ ] **Step 3: Run tests; verify they fail**

```bash
dotnet test Tests~/PureCSharp/CardCore.PureTests.csproj --filter "FullyQualifiedName~CardCore.PureTests.PlayerTests"
```

Expected: compile errors (no `DiscardPile` property, no 3-arg `Player` ctor).

- [ ] **Step 4: Update `Runtime/Models/Player.cs`**

Replace the contents of `Runtime/Models/Player.cs` with EXACTLY this:

```csharp
using System;

namespace CardCore;

public sealed class Player
{
    public int Id { get; }
    public Hand Hand { get; }
    public DiscardPile DiscardPile { get; }

    public Player(int id) : this(id, new Hand(), new DiscardPile()) { }

    [Newtonsoft.Json.JsonConstructor]
    internal Player(int id, Hand hand, DiscardPile? discardPile)
    {
        if (id < 0) throw new ArgumentException("Player.Id must be >= 0.", nameof(id));
        Id = id;
        Hand = hand ?? new Hand();
        DiscardPile = discardPile ?? new DiscardPile();
    }
}
```

- [ ] **Step 5: Run tests; verify they pass**

```bash
dotnet test Tests~/PureCSharp/CardCore.PureTests.csproj --filter "FullyQualifiedName~CardCore.PureTests.PlayerTests"
```

Expected: all `PlayerTests` (existing + 3 new) pass.

Run the full suite to confirm no regressions:

```bash
dotnet test Tests~/PureCSharp/CardCore.PureTests.csproj
```

Expected: baseline + 3 new tests, all green.

- [ ] **Step 6: Stage**

```bash
git add Runtime/Models/Player.cs Tests~/PureCSharp/PlayerTests.cs
git status
```

Stop and await user commit.

---

## Task 3: `Deck.AddRange` and `Deck.ReorderTo` — internal helpers + tests

**Files:**
- Modify: `Runtime/Models/Deck.cs`
- Modify: `Tests~/PureCSharp/DeckTests.cs`

Two new `internal` methods on `Deck`. `AddRange` appends cards in order (used by `ApplyDiscardMovedToDeck` after asserting the deck is empty). `ReorderTo` validates that an id list matches the deck's contents (same set, same length) and rearranges in-place (used by `ApplyDeckShuffled`).

`internal` is fine because tests already have `InternalsVisibleTo("CardCore.PureTests")` access (the existing `Deck` ctor is `internal` and tests use it).

- [ ] **Step 1: Read the current `DeckTests.cs` to find the bottom of the test class**

```bash
cat Tests~/PureCSharp/DeckTests.cs
```

- [ ] **Step 2: Append failing tests to `DeckTests.cs`**

Add these inside the existing `DeckTests` class:

```csharp
    [Fact]
    public void AddRange_AppendsInOrder()
    {
        var a = NewCard("a");
        var b = NewCard("b");
        var c = NewCard("c");
        var deck = new Deck(new List<CardInstance> { a });

        deck.AddRange(new List<CardInstance> { b, c });

        Assert.Equal(3, deck.Count);
        Assert.Equal(a.InstanceId, deck[0].InstanceId);
        Assert.Equal(b.InstanceId, deck[1].InstanceId);
        Assert.Equal(c.InstanceId, deck[2].InstanceId);
    }

    [Fact]
    public void AddRange_Null_Throws()
    {
        var deck = new Deck(new List<CardInstance>());
        Assert.Throws<ArgumentNullException>(() => deck.AddRange(null!));
    }

    [Fact]
    public void ReorderTo_ValidIdList_RearrangesInPlace()
    {
        var a = NewCard("a");
        var b = NewCard("b");
        var c = NewCard("c");
        var deck = new Deck(new List<CardInstance> { a, b, c });

        deck.ReorderTo(new List<Guid> { c.InstanceId, a.InstanceId, b.InstanceId });

        Assert.Equal(c.InstanceId, deck[0].InstanceId);
        Assert.Equal(a.InstanceId, deck[1].InstanceId);
        Assert.Equal(b.InstanceId, deck[2].InstanceId);
    }

    [Fact]
    public void ReorderTo_LengthMismatch_Throws()
    {
        var a = NewCard("a");
        var b = NewCard("b");
        var deck = new Deck(new List<CardInstance> { a, b });

        Assert.Throws<InvalidOperationException>(
            () => deck.ReorderTo(new List<Guid> { a.InstanceId }));
    }

    [Fact]
    public void ReorderTo_UnknownId_Throws()
    {
        var a = NewCard("a");
        var b = NewCard("b");
        var deck = new Deck(new List<CardInstance> { a, b });

        Assert.Throws<InvalidOperationException>(
            () => deck.ReorderTo(new List<Guid> { a.InstanceId, Guid.NewGuid() }));
    }

    [Fact]
    public void ReorderTo_Null_Throws()
    {
        var deck = new Deck(new List<CardInstance>());
        Assert.Throws<ArgumentNullException>(() => deck.ReorderTo(null!));
    }
```

If `DeckTests.cs` doesn't already have a `NewCard` helper or `using System;` / `using System.Collections.Generic;`, add them at the top of the file. Check `cat` output from Step 1 to be sure.

- [ ] **Step 3: Run tests; verify they fail**

```bash
dotnet test Tests~/PureCSharp/CardCore.PureTests.csproj --filter "FullyQualifiedName~CardCore.PureTests.DeckTests"
```

Expected: compile errors (`AddRange` and `ReorderTo` don't exist on `Deck`).

- [ ] **Step 4: Update `Runtime/Models/Deck.cs`**

Open `Runtime/Models/Deck.cs`. Add these two methods inside the `Deck` class (between `Snapshot()` and the private `Shuffle` helper):

```csharp
    internal void AddRange(IReadOnlyList<CardInstance> cards)
    {
        if (cards is null) throw new ArgumentNullException(nameof(cards));
        foreach (var c in cards)
        {
            if (c is null) throw new ArgumentException("Deck.AddRange: cards must not contain nulls.", nameof(cards));
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
```

- [ ] **Step 5: Run tests; verify they pass**

```bash
dotnet test Tests~/PureCSharp/CardCore.PureTests.csproj --filter "FullyQualifiedName~CardCore.PureTests.DeckTests"
```

Expected: all `DeckTests` (existing + 6 new) pass.

Run the full suite:

```bash
dotnet test Tests~/PureCSharp/CardCore.PureTests.csproj
```

Expected: no regressions.

- [ ] **Step 6: Stage**

```bash
git add Runtime/Models/Deck.cs Tests~/PureCSharp/DeckTests.cs
git status
```

Stop and await user commit.

---

## Task 4: New event records (4 files)

**Files:**
- Create: `Runtime/Events/CardDiscarded.cs`
- Create: `Runtime/Events/CardDestroyed.cs`
- Create: `Runtime/Events/DiscardMovedToDeck.cs`
- Create: `Runtime/Events/DeckShuffled.cs`

Plain record types — no behavior, no tests of their own (their behavior is tested through commands and apply paths). Build verification only.

- [ ] **Step 1: Create `Runtime/Events/CardDiscarded.cs`**

```csharp
using System;

namespace CardCore.Events;

public sealed record CardDiscarded : GameEvent
{
    public int PlayerId { get; init; }
    public Guid InstanceId { get; init; }
    public int HandIndexBefore { get; init; }
}
```

- [ ] **Step 2: Create `Runtime/Events/CardDestroyed.cs`**

```csharp
using System;

namespace CardCore.Events;

public sealed record CardDestroyed : GameEvent
{
    public int PlayerId { get; init; }
    public Guid InstanceId { get; init; }
    public int HandIndexBefore { get; init; }
}
```

- [ ] **Step 3: Create `Runtime/Events/DiscardMovedToDeck.cs`**

```csharp
using System;
using System.Collections.Generic;

namespace CardCore.Events;

public sealed record DiscardMovedToDeck : GameEvent
{
    public int PlayerId { get; init; }
    public IReadOnlyList<Guid> InstanceIds { get; init; } = Array.Empty<Guid>();
}
```

- [ ] **Step 4: Create `Runtime/Events/DeckShuffled.cs`**

```csharp
using System;
using System.Collections.Generic;

namespace CardCore.Events;

public sealed record DeckShuffled : GameEvent
{
    public int PlayerId { get; init; }
    public IReadOnlyList<Guid> PostShuffleInstanceIds { get; init; } = Array.Empty<Guid>();
}
```

- [ ] **Step 5: Build — confirm zero compile errors**

```bash
dotnet build Runtime/CardCore.csproj
```

Expected: build succeeds. The new event types compile but aren't wired into anything yet.

- [ ] **Step 6: Stage**

```bash
git add Runtime/Events/CardDiscarded.cs Runtime/Events/CardDestroyed.cs Runtime/Events/DiscardMovedToDeck.cs Runtime/Events/DeckShuffled.cs
git status
```

Stop and await user commit.

---

## Task 5: Wire 4 new events into `GameEventConverter` + JSON round-trip tests

**Files:**
- Modify: `Runtime/GameEvent.cs`
- Modify: `Tests~/PureCSharp/GameEventTests.cs`

Without this wiring, the polymorphic deserializer throws "Unknown GameEvent subtype" for the new events.

- [ ] **Step 1: Read the current `GameEventTests.cs` to see the round-trip pattern**

```bash
cat Tests~/PureCSharp/GameEventTests.cs
```

Note the existing round-trip test pattern (typically: build event, `JsonConvert.SerializeObject(evt, GameEvent.JsonSettings)`, `JsonConvert.DeserializeObject<GameEvent>(json, GameEvent.JsonSettings)`, assert `IsType<T>` and field equality).

- [ ] **Step 2: Append failing tests to `GameEventTests.cs`**

Add these inside the existing test class:

```csharp
    [Fact]
    public void CardDiscarded_RoundTripsThroughPolymorphicConverter()
    {
        var id = Guid.NewGuid();
        var evt = new CardDiscarded { SequenceId = 7, Timestamp = 100, PlayerId = 0, InstanceId = id, HandIndexBefore = 2 };

        var json = JsonConvert.SerializeObject(evt, GameEvent.JsonSettings);
        var roundTripped = JsonConvert.DeserializeObject<GameEvent>(json, GameEvent.JsonSettings);

        var typed = Assert.IsType<CardDiscarded>(roundTripped);
        Assert.Equal(7, typed.SequenceId);
        Assert.Equal(0, typed.PlayerId);
        Assert.Equal(id, typed.InstanceId);
        Assert.Equal(2, typed.HandIndexBefore);
    }

    [Fact]
    public void CardDestroyed_RoundTripsThroughPolymorphicConverter()
    {
        var id = Guid.NewGuid();
        var evt = new CardDestroyed { SequenceId = 8, Timestamp = 100, PlayerId = 1, InstanceId = id, HandIndexBefore = 0 };

        var json = JsonConvert.SerializeObject(evt, GameEvent.JsonSettings);
        var roundTripped = JsonConvert.DeserializeObject<GameEvent>(json, GameEvent.JsonSettings);

        var typed = Assert.IsType<CardDestroyed>(roundTripped);
        Assert.Equal(8, typed.SequenceId);
        Assert.Equal(1, typed.PlayerId);
        Assert.Equal(id, typed.InstanceId);
        Assert.Equal(0, typed.HandIndexBefore);
    }

    [Fact]
    public void DiscardMovedToDeck_RoundTripsThroughPolymorphicConverter()
    {
        var ids = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var evt = new DiscardMovedToDeck { SequenceId = 9, Timestamp = 100, PlayerId = 0, InstanceIds = ids };

        var json = JsonConvert.SerializeObject(evt, GameEvent.JsonSettings);
        var roundTripped = JsonConvert.DeserializeObject<GameEvent>(json, GameEvent.JsonSettings);

        var typed = Assert.IsType<DiscardMovedToDeck>(roundTripped);
        Assert.Equal(0, typed.PlayerId);
        Assert.Equal(ids.Count, typed.InstanceIds.Count);
        Assert.Equal(ids[0], typed.InstanceIds[0]);
        Assert.Equal(ids[2], typed.InstanceIds[2]);
    }

    [Fact]
    public void DeckShuffled_RoundTripsThroughPolymorphicConverter()
    {
        var ids = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var evt = new DeckShuffled { SequenceId = 10, Timestamp = 100, PlayerId = 0, PostShuffleInstanceIds = ids };

        var json = JsonConvert.SerializeObject(evt, GameEvent.JsonSettings);
        var roundTripped = JsonConvert.DeserializeObject<GameEvent>(json, GameEvent.JsonSettings);

        var typed = Assert.IsType<DeckShuffled>(roundTripped);
        Assert.Equal(2, typed.PostShuffleInstanceIds.Count);
        Assert.Equal(ids[0], typed.PostShuffleInstanceIds[0]);
    }
```

If `GameEventTests.cs` doesn't already have these usings at the top, add them:

```csharp
using System;
using System.Collections.Generic;
using CardCore.Events;
using Newtonsoft.Json;
```

- [ ] **Step 3: Run tests; verify they fail**

```bash
dotnet test Tests~/PureCSharp/CardCore.PureTests.csproj --filter "FullyQualifiedName~CardCore.PureTests.GameEventTests"
```

Expected: 4 new tests fail with `JsonSerializationException: Unknown GameEvent subtype` (or similar) on deserialization. The converter doesn't know the new types yet.

- [ ] **Step 4: Add the new entries to the converter switch in `GameEvent.cs`**

Open `Runtime/GameEvent.cs`. Find the `typeName switch` block in `GameEventConverter.ReadJson` — currently:

```csharp
System.Type concrete = typeName switch
{
    nameof(GameStarted) => typeof(GameStarted),
    nameof(CardDrawn)   => typeof(CardDrawn),
    nameof(CardPlayed)  => typeof(CardPlayed),
    _ => throw new JsonSerializationException($"Unknown GameEvent subtype: {typeName}"),
};
```

Replace it with EXACTLY:

```csharp
System.Type concrete = typeName switch
{
    nameof(GameStarted)        => typeof(GameStarted),
    nameof(CardDrawn)          => typeof(CardDrawn),
    nameof(CardPlayed)         => typeof(CardPlayed),
    nameof(CardDiscarded)      => typeof(CardDiscarded),
    nameof(CardDestroyed)      => typeof(CardDestroyed),
    nameof(DiscardMovedToDeck) => typeof(DiscardMovedToDeck),
    nameof(DeckShuffled)       => typeof(DeckShuffled),
    _ => throw new JsonSerializationException($"Unknown GameEvent subtype: {typeName}"),
};
```

- [ ] **Step 5: Run tests; verify they pass**

```bash
dotnet test Tests~/PureCSharp/CardCore.PureTests.csproj --filter "FullyQualifiedName~CardCore.PureTests.GameEventTests"
```

Expected: all `GameEventTests` pass.

Run the full suite:

```bash
dotnet test Tests~/PureCSharp/CardCore.PureTests.csproj
```

Expected: no regressions.

- [ ] **Step 6: Stage**

```bash
git add Runtime/GameEvent.cs Tests~/PureCSharp/GameEventTests.cs
git status
```

Stop and await user commit.

---

## Task 6: `DiscardCommand` — TDD

**Files:**
- Create: `Tests~/PureCSharp/DiscardCommandTests.cs`
- Create: `Runtime/Commands/DiscardCommand.cs`

`DiscardCommand(int playerId, Guid instanceId)`. `CanExecute` requires the card to exist in the player's hand. `Execute` emits one `CardDiscarded` with `HandIndexBefore`. State mutation (hand → discard pile) happens in `ApplyCardDiscarded`, which is added in Task 10.

Until Task 10 lands, `Execute` will emit a valid event but `GameState.Apply` will throw `InvalidOperationException` (unknown event type). That's fine for this task's unit tests because they call `Execute` directly, not through the engine.

- [ ] **Step 1: Write the failing tests**

Create `Tests~/PureCSharp/DiscardCommandTests.cs` with EXACTLY this content:

```csharp
using System;
using CardCore;
using CardCore.Commands;
using CardCore.Events;
using Xunit;

namespace CardCore.PureTests;

public class DiscardCommandTests
{
    private static CardInstance NewCard(string defId = "c") =>
        CardInstance.From(new CardDefinition(defId));

    private static GameState StartedStateWithHand(params CardInstance[] cards)
    {
        var s = new GameState();
        s.ApplyForTest(new GameStarted
        {
            SequenceId = 0, Timestamp = 0,
            InitialDeckOrder = cards, PlayerCount = 1, Seed = 0,
        });
        for (int i = 0; i < cards.Length; i++)
        {
            s.ApplyForTest(new CardDrawn
            {
                SequenceId = i + 1, Timestamp = 0,
                PlayerId = 0, InstanceId = cards[i].InstanceId, DeckIndexBefore = 0,
            });
        }
        return s;
    }

    [Fact]
    public void Constructor_NegativePlayerId_Throws()
    {
        Assert.Throws<ArgumentException>(() => new DiscardCommand(-1, Guid.NewGuid()));
    }

    [Fact]
    public void Constructor_EmptyGuid_Throws()
    {
        Assert.Throws<ArgumentException>(() => new DiscardCommand(0, Guid.Empty));
    }

    [Fact]
    public void CanExecute_GameNotStarted_False()
    {
        var cmd = new DiscardCommand(0, Guid.NewGuid());
        Assert.False(cmd.CanExecute(new GameState()));
    }

    [Fact]
    public void CanExecute_InvalidPlayerId_False()
    {
        var card = NewCard("a");
        var s = StartedStateWithHand(card);
        var cmd = new DiscardCommand(5, card.InstanceId);
        Assert.False(cmd.CanExecute(s));
    }

    [Fact]
    public void CanExecute_InstanceNotInHand_False()
    {
        var card = NewCard("a");
        var s = StartedStateWithHand(card);
        var cmd = new DiscardCommand(0, Guid.NewGuid());
        Assert.False(cmd.CanExecute(s));
    }

    [Fact]
    public void CanExecute_HappyPath_True()
    {
        var card = NewCard("a");
        var s = StartedStateWithHand(card);
        var cmd = new DiscardCommand(0, card.InstanceId);
        Assert.True(cmd.CanExecute(s));
    }

    [Fact]
    public void Execute_EmitsSingleCardDiscardedEvent_WithCorrectHandIndex()
    {
        var a = NewCard("a");
        var b = NewCard("b");
        var c = NewCard("c");
        var s = StartedStateWithHand(a, b, c);

        var cmd = new DiscardCommand(0, b.InstanceId);
        var events = cmd.Execute(s);

        Assert.Single(events);
        var typed = Assert.IsType<CardDiscarded>(events[0]);
        Assert.Equal(0, typed.PlayerId);
        Assert.Equal(b.InstanceId, typed.InstanceId);
        Assert.Equal(1, typed.HandIndexBefore);
    }
}
```

- [ ] **Step 2: Run tests; verify they fail**

```bash
dotnet test Tests~/PureCSharp/CardCore.PureTests.csproj --filter "FullyQualifiedName~CardCore.PureTests.DiscardCommandTests"
```

Expected: compile errors — `DiscardCommand` doesn't exist.

- [ ] **Step 3: Create `Runtime/Commands/DiscardCommand.cs`**

Create `Runtime/Commands/DiscardCommand.cs` with EXACTLY this content:

```csharp
using System;
using System.Collections.Generic;
using CardCore.Events;

namespace CardCore.Commands;

public sealed class DiscardCommand : IGameCommand
{
    private readonly int _playerId;
    private readonly Guid _instanceId;

    public DiscardCommand(int playerId, Guid instanceId)
    {
        if (playerId < 0)
            throw new ArgumentException("playerId must be >= 0.", nameof(playerId));
        if (instanceId == Guid.Empty)
            throw new ArgumentException("instanceId must not be Guid.Empty.", nameof(instanceId));
        _playerId = playerId;
        _instanceId = instanceId;
    }

    public bool CanExecute(GameState state)
    {
        if (!state.IsStarted) return false;
        if (_playerId < 0 || _playerId >= state.Players.Count) return false;
        return FindHandIndex(state) >= 0;
    }

    public IReadOnlyList<GameEvent> Execute(GameState state)
    {
        int handIndex = FindHandIndex(state);
        return new GameEvent[]
        {
            new CardDiscarded
            {
                PlayerId = _playerId,
                InstanceId = _instanceId,
                HandIndexBefore = handIndex,
            }
        };
    }

    private int FindHandIndex(GameState state)
    {
        var hand = state.Players[_playerId].Hand;
        for (int i = 0; i < hand.Count; i++)
        {
            if (hand[i].InstanceId == _instanceId) return i;
        }
        return -1;
    }
}
```

- [ ] **Step 4: Run tests; verify they pass**

```bash
dotnet test Tests~/PureCSharp/CardCore.PureTests.csproj --filter "FullyQualifiedName~CardCore.PureTests.DiscardCommandTests"
```

Expected: all 7 pass.

- [ ] **Step 5: Stage**

```bash
git add Runtime/Commands/DiscardCommand.cs Tests~/PureCSharp/DiscardCommandTests.cs
git status
```

Stop and await user commit.

---

## Task 7: `DestroyCardCommand` — TDD

**Files:**
- Create: `Tests~/PureCSharp/DestroyCardCommandTests.cs`
- Create: `Runtime/Commands/DestroyCardCommand.cs`

Mirror of `DiscardCommand`. Same shape, same validation. Only difference: emits `CardDestroyed`.

- [ ] **Step 1: Write the failing tests**

Create `Tests~/PureCSharp/DestroyCardCommandTests.cs` with EXACTLY this content:

```csharp
using System;
using CardCore;
using CardCore.Commands;
using CardCore.Events;
using Xunit;

namespace CardCore.PureTests;

public class DestroyCardCommandTests
{
    private static CardInstance NewCard(string defId = "c") =>
        CardInstance.From(new CardDefinition(defId));

    private static GameState StartedStateWithHand(params CardInstance[] cards)
    {
        var s = new GameState();
        s.ApplyForTest(new GameStarted
        {
            SequenceId = 0, Timestamp = 0,
            InitialDeckOrder = cards, PlayerCount = 1, Seed = 0,
        });
        for (int i = 0; i < cards.Length; i++)
        {
            s.ApplyForTest(new CardDrawn
            {
                SequenceId = i + 1, Timestamp = 0,
                PlayerId = 0, InstanceId = cards[i].InstanceId, DeckIndexBefore = 0,
            });
        }
        return s;
    }

    [Fact]
    public void Constructor_NegativePlayerId_Throws()
    {
        Assert.Throws<ArgumentException>(() => new DestroyCardCommand(-1, Guid.NewGuid()));
    }

    [Fact]
    public void Constructor_EmptyGuid_Throws()
    {
        Assert.Throws<ArgumentException>(() => new DestroyCardCommand(0, Guid.Empty));
    }

    [Fact]
    public void CanExecute_GameNotStarted_False()
    {
        var cmd = new DestroyCardCommand(0, Guid.NewGuid());
        Assert.False(cmd.CanExecute(new GameState()));
    }

    [Fact]
    public void CanExecute_InvalidPlayerId_False()
    {
        var card = NewCard("a");
        var s = StartedStateWithHand(card);
        var cmd = new DestroyCardCommand(5, card.InstanceId);
        Assert.False(cmd.CanExecute(s));
    }

    [Fact]
    public void CanExecute_InstanceNotInHand_False()
    {
        var card = NewCard("a");
        var s = StartedStateWithHand(card);
        var cmd = new DestroyCardCommand(0, Guid.NewGuid());
        Assert.False(cmd.CanExecute(s));
    }

    [Fact]
    public void CanExecute_HappyPath_True()
    {
        var card = NewCard("a");
        var s = StartedStateWithHand(card);
        var cmd = new DestroyCardCommand(0, card.InstanceId);
        Assert.True(cmd.CanExecute(s));
    }

    [Fact]
    public void Execute_EmitsSingleCardDestroyedEvent_WithCorrectHandIndex()
    {
        var a = NewCard("a");
        var b = NewCard("b");
        var s = StartedStateWithHand(a, b);

        var cmd = new DestroyCardCommand(0, a.InstanceId);
        var events = cmd.Execute(s);

        Assert.Single(events);
        var typed = Assert.IsType<CardDestroyed>(events[0]);
        Assert.Equal(0, typed.PlayerId);
        Assert.Equal(a.InstanceId, typed.InstanceId);
        Assert.Equal(0, typed.HandIndexBefore);
    }
}
```

- [ ] **Step 2: Run tests; verify they fail**

```bash
dotnet test Tests~/PureCSharp/CardCore.PureTests.csproj --filter "FullyQualifiedName~CardCore.PureTests.DestroyCardCommandTests"
```

Expected: compile errors.

- [ ] **Step 3: Create `Runtime/Commands/DestroyCardCommand.cs`**

Create `Runtime/Commands/DestroyCardCommand.cs` with EXACTLY this content:

```csharp
using System;
using System.Collections.Generic;
using CardCore.Events;

namespace CardCore.Commands;

public sealed class DestroyCardCommand : IGameCommand
{
    private readonly int _playerId;
    private readonly Guid _instanceId;

    public DestroyCardCommand(int playerId, Guid instanceId)
    {
        if (playerId < 0)
            throw new ArgumentException("playerId must be >= 0.", nameof(playerId));
        if (instanceId == Guid.Empty)
            throw new ArgumentException("instanceId must not be Guid.Empty.", nameof(instanceId));
        _playerId = playerId;
        _instanceId = instanceId;
    }

    public bool CanExecute(GameState state)
    {
        if (!state.IsStarted) return false;
        if (_playerId < 0 || _playerId >= state.Players.Count) return false;
        return FindHandIndex(state) >= 0;
    }

    public IReadOnlyList<GameEvent> Execute(GameState state)
    {
        int handIndex = FindHandIndex(state);
        return new GameEvent[]
        {
            new CardDestroyed
            {
                PlayerId = _playerId,
                InstanceId = _instanceId,
                HandIndexBefore = handIndex,
            }
        };
    }

    private int FindHandIndex(GameState state)
    {
        var hand = state.Players[_playerId].Hand;
        for (int i = 0; i < hand.Count; i++)
        {
            if (hand[i].InstanceId == _instanceId) return i;
        }
        return -1;
    }
}
```

- [ ] **Step 4: Run tests; verify they pass**

```bash
dotnet test Tests~/PureCSharp/CardCore.PureTests.csproj --filter "FullyQualifiedName~CardCore.PureTests.DestroyCardCommandTests"
```

Expected: all 7 pass.

- [ ] **Step 5: Stage**

```bash
git add Runtime/Commands/DestroyCardCommand.cs Tests~/PureCSharp/DestroyCardCommandTests.cs
git status
```

Stop and await user commit.

---

## Task 8: `MoveDiscardToDeckCommand` — TDD

**Files:**
- Create: `Tests~/PureCSharp/MoveDiscardToDeckCommandTests.cs`
- Create: `Runtime/Commands/MoveDiscardToDeckCommand.cs`

`MoveDiscardToDeckCommand(int playerId)`. `CanExecute` requires: game started, valid player, deck empty, discard pile non-empty. `Execute` emits one `DiscardMovedToDeck` whose `InstanceIds` payload is the discard pile's contents in order.

Test setup for this command needs a player whose hand was drained into the discard pile via `CardDiscarded` events — exercises the not-yet-implemented apply path from Task 10. To keep this task self-contained (apply isn't in yet), tests construct state by direct `ApplyForTest` calls — but `ApplyForTest` of `CardDiscarded` will throw until Task 10. So tests in this task use a helper that hand-builds state via reflection, OR we accept that the order is: ship apply paths *before* the commands that depend on them in tests.

Order decision: **Task 10 lands the four apply paths first; Tasks 11+ test the commands end-to-end through `ApplyForTest`.** This Task 8 tests the *command* in isolation — it constructs a state where the player has a discard pile by mutating the `DiscardPile` directly through public `Add()`. That's legitimate test setup; the command doesn't care how the pile got populated.

- [ ] **Step 1: Write the failing tests**

Create `Tests~/PureCSharp/MoveDiscardToDeckCommandTests.cs` with EXACTLY this content:

```csharp
using System;
using System.Collections.Generic;
using CardCore;
using CardCore.Commands;
using CardCore.Events;
using Xunit;

namespace CardCore.PureTests;

public class MoveDiscardToDeckCommandTests
{
    private static CardInstance NewCard(string defId = "c") =>
        CardInstance.From(new CardDefinition(defId));

    // Builds a started state with an empty deck and the given cards loaded
    // directly into the player's discard pile. Skips the engine entirely;
    // exercises only the apply paths that already exist.
    private static GameState StartedStateWithDiscardPile(params CardInstance[] discardContents)
    {
        var s = new GameState();
        // Start with one card so GameStarted has a non-empty deck (it requires non-empty).
        var seedCard = NewCard("seed");
        s.ApplyForTest(new GameStarted
        {
            SequenceId = 0, Timestamp = 0,
            InitialDeckOrder = new[] { seedCard }, PlayerCount = 1, Seed = 0,
        });
        // Drain the deck so it's empty.
        s.ApplyForTest(new CardDrawn
        {
            SequenceId = 1, Timestamp = 0,
            PlayerId = 0, InstanceId = seedCard.InstanceId, DeckIndexBefore = 0,
        });
        // Hand the seed card to the play area so it's not in hand.
        s.ApplyForTest(new CardPlayed
        {
            SequenceId = 2, Timestamp = 0,
            PlayerId = 0, InstanceId = seedCard.InstanceId,
            HandIndexBefore = 0, PlayAreaIndexAfter = 0,
        });
        // Manually populate the discard pile via its public API.
        foreach (var c in discardContents)
        {
            s.Players[0].DiscardPile.Add(c);
        }
        return s;
    }

    [Fact]
    public void Constructor_NegativePlayerId_Throws()
    {
        Assert.Throws<ArgumentException>(() => new MoveDiscardToDeckCommand(-1));
    }

    [Fact]
    public void CanExecute_GameNotStarted_False()
    {
        var cmd = new MoveDiscardToDeckCommand(0);
        Assert.False(cmd.CanExecute(new GameState()));
    }

    [Fact]
    public void CanExecute_InvalidPlayerId_False()
    {
        var s = StartedStateWithDiscardPile(NewCard("a"));
        var cmd = new MoveDiscardToDeckCommand(5);
        Assert.False(cmd.CanExecute(s));
    }

    [Fact]
    public void CanExecute_DeckNotEmpty_False()
    {
        var s = new GameState();
        var seedCard = NewCard("seed");
        s.ApplyForTest(new GameStarted
        {
            SequenceId = 0, Timestamp = 0,
            InitialDeckOrder = new[] { seedCard }, PlayerCount = 1, Seed = 0,
        });
        // Deck still has the seed card. Populate discard via public API.
        s.Players[0].DiscardPile.Add(NewCard("d"));

        var cmd = new MoveDiscardToDeckCommand(0);
        Assert.False(cmd.CanExecute(s));
    }

    [Fact]
    public void CanExecute_DiscardEmpty_False()
    {
        var s = StartedStateWithDiscardPile(/* no cards */);
        var cmd = new MoveDiscardToDeckCommand(0);
        Assert.False(cmd.CanExecute(s));
    }

    [Fact]
    public void CanExecute_HappyPath_True()
    {
        var s = StartedStateWithDiscardPile(NewCard("a"), NewCard("b"));
        var cmd = new MoveDiscardToDeckCommand(0);
        Assert.True(cmd.CanExecute(s));
    }

    [Fact]
    public void Execute_EmitsDiscardMovedToDeck_WithIdsInPileOrder()
    {
        var a = NewCard("a");
        var b = NewCard("b");
        var c = NewCard("c");
        var s = StartedStateWithDiscardPile(a, b, c);

        var cmd = new MoveDiscardToDeckCommand(0);
        var events = cmd.Execute(s);

        Assert.Single(events);
        var typed = Assert.IsType<DiscardMovedToDeck>(events[0]);
        Assert.Equal(0, typed.PlayerId);
        Assert.Equal(3, typed.InstanceIds.Count);
        Assert.Equal(a.InstanceId, typed.InstanceIds[0]);
        Assert.Equal(b.InstanceId, typed.InstanceIds[1]);
        Assert.Equal(c.InstanceId, typed.InstanceIds[2]);
    }
}
```

- [ ] **Step 2: Run tests; verify they fail**

```bash
dotnet test Tests~/PureCSharp/CardCore.PureTests.csproj --filter "FullyQualifiedName~CardCore.PureTests.MoveDiscardToDeckCommandTests"
```

Expected: compile errors.

- [ ] **Step 3: Create `Runtime/Commands/MoveDiscardToDeckCommand.cs`**

Create `Runtime/Commands/MoveDiscardToDeckCommand.cs` with EXACTLY this content:

```csharp
using System;
using System.Collections.Generic;
using CardCore.Events;

namespace CardCore.Commands;

public sealed class MoveDiscardToDeckCommand : IGameCommand
{
    private readonly int _playerId;

    public MoveDiscardToDeckCommand(int playerId)
    {
        if (playerId < 0)
            throw new ArgumentException("playerId must be >= 0.", nameof(playerId));
        _playerId = playerId;
    }

    public bool CanExecute(GameState state)
    {
        if (!state.IsStarted) return false;
        if (_playerId < 0 || _playerId >= state.Players.Count) return false;
        if (state.Deck is null || state.Deck.Count != 0) return false;
        if (state.Players[_playerId].DiscardPile.Count == 0) return false;
        return true;
    }

    public IReadOnlyList<GameEvent> Execute(GameState state)
    {
        var pile = state.Players[_playerId].DiscardPile;
        var ids = new List<Guid>(pile.Count);
        for (int i = 0; i < pile.Count; i++)
        {
            ids.Add(pile[i].InstanceId);
        }
        return new GameEvent[]
        {
            new DiscardMovedToDeck
            {
                PlayerId = _playerId,
                InstanceIds = ids,
            }
        };
    }
}
```

- [ ] **Step 4: Run tests; verify they pass**

```bash
dotnet test Tests~/PureCSharp/CardCore.PureTests.csproj --filter "FullyQualifiedName~CardCore.PureTests.MoveDiscardToDeckCommandTests"
```

Expected: all 7 pass.

- [ ] **Step 5: Stage**

```bash
git add Runtime/Commands/MoveDiscardToDeckCommand.cs Tests~/PureCSharp/MoveDiscardToDeckCommandTests.cs
git status
```

Stop and await user commit.

---

## Task 9: `ShuffleDeckCommand` — TDD

**Files:**
- Create: `Tests~/PureCSharp/ShuffleDeckCommandTests.cs`
- Create: `Runtime/Commands/ShuffleDeckCommand.cs`

`ShuffleDeckCommand(int playerId)`. `CanExecute` requires: game started, valid player, deck non-empty. `Execute` uses a fresh `new Random()` (unseeded) and emits one `DeckShuffled` whose `PostShuffleInstanceIds` is the shuffled order — the event becomes the source of truth, replay uses `Deck.ReorderTo` to match.

Probabilistic test for "shuffle changed something" — over 5 trials with an 18-card deck, vanishingly small chance all 5 are identical to the input order. Use `Assert.False(allFive == input)`.

- [ ] **Step 1: Write the failing tests**

Create `Tests~/PureCSharp/ShuffleDeckCommandTests.cs` with EXACTLY this content:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.Commands;
using CardCore.Events;
using Xunit;

namespace CardCore.PureTests;

public class ShuffleDeckCommandTests
{
    private static CardInstance NewCard(string defId = "c") =>
        CardInstance.From(new CardDefinition(defId));

    private static GameState StartedStateWithDeck(params CardInstance[] deck)
    {
        var s = new GameState();
        s.ApplyForTest(new GameStarted
        {
            SequenceId = 0, Timestamp = 0,
            InitialDeckOrder = deck, PlayerCount = 1, Seed = 0,
        });
        return s;
    }

    [Fact]
    public void Constructor_NegativePlayerId_Throws()
    {
        Assert.Throws<ArgumentException>(() => new ShuffleDeckCommand(-1));
    }

    [Fact]
    public void CanExecute_GameNotStarted_False()
    {
        Assert.False(new ShuffleDeckCommand(0).CanExecute(new GameState()));
    }

    [Fact]
    public void CanExecute_InvalidPlayerId_False()
    {
        var s = StartedStateWithDeck(NewCard("a"));
        Assert.False(new ShuffleDeckCommand(5).CanExecute(s));
    }

    [Fact]
    public void CanExecute_EmptyDeck_False()
    {
        var s = StartedStateWithDeck(/* nothing */);
        // GameStarted requires non-empty deck so use a different setup: start, drain.
        var seedCard = NewCard("seed");
        s = StartedStateWithDeck(seedCard);
        s.ApplyForTest(new CardDrawn
        {
            SequenceId = 1, Timestamp = 0,
            PlayerId = 0, InstanceId = seedCard.InstanceId, DeckIndexBefore = 0,
        });
        Assert.False(new ShuffleDeckCommand(0).CanExecute(s));
    }

    [Fact]
    public void CanExecute_DeckHasCards_True()
    {
        var s = StartedStateWithDeck(NewCard("a"), NewCard("b"));
        Assert.True(new ShuffleDeckCommand(0).CanExecute(s));
    }

    [Fact]
    public void Execute_EmitsDeckShuffled_WithSameIdSet()
    {
        var cards = new[]
        {
            NewCard("a"), NewCard("b"), NewCard("c"), NewCard("d"),
            NewCard("e"), NewCard("f"), NewCard("g"), NewCard("h"),
        };
        var s = StartedStateWithDeck(cards);

        var cmd = new ShuffleDeckCommand(0);
        var events = cmd.Execute(s);

        Assert.Single(events);
        var typed = Assert.IsType<DeckShuffled>(events[0]);
        Assert.Equal(0, typed.PlayerId);
        Assert.Equal(cards.Length, typed.PostShuffleInstanceIds.Count);

        var inputIds = new HashSet<Guid>(cards.Select(c => c.InstanceId));
        var outputIds = new HashSet<Guid>(typed.PostShuffleInstanceIds);
        Assert.Equal(inputIds, outputIds);
    }

    [Fact]
    public void Execute_MultipleRuns_ProduceDifferentOrderings()
    {
        // Probabilistic: with 18 cards and 5 trials, the chance of all 5 being
        // byte-identical to the input is ~ 5 * (1/18!), vanishingly small.
        var cards = new List<CardInstance>();
        for (int i = 0; i < 18; i++) cards.Add(NewCard($"c{i}"));
        var s = StartedStateWithDeck(cards.ToArray());
        var input = cards.Select(c => c.InstanceId).ToArray();
        var cmd = new ShuffleDeckCommand(0);

        bool sawDifferent = false;
        for (int trial = 0; trial < 5; trial++)
        {
            var evt = (DeckShuffled)cmd.Execute(s)[0];
            if (!evt.PostShuffleInstanceIds.SequenceEqual(input))
            {
                sawDifferent = true;
                break;
            }
        }
        Assert.True(sawDifferent, "5 shuffles produced the same ordering as input — RNG broken or shuffle is a no-op.");
    }
}
```

- [ ] **Step 2: Run tests; verify they fail**

```bash
dotnet test Tests~/PureCSharp/CardCore.PureTests.csproj --filter "FullyQualifiedName~CardCore.PureTests.ShuffleDeckCommandTests"
```

Expected: compile errors.

- [ ] **Step 3: Create `Runtime/Commands/ShuffleDeckCommand.cs`**

Create `Runtime/Commands/ShuffleDeckCommand.cs` with EXACTLY this content:

```csharp
using System;
using System.Collections.Generic;
using CardCore.Events;

namespace CardCore.Commands;

public sealed class ShuffleDeckCommand : IGameCommand
{
    private readonly int _playerId;

    public ShuffleDeckCommand(int playerId)
    {
        if (playerId < 0)
            throw new ArgumentException("playerId must be >= 0.", nameof(playerId));
        _playerId = playerId;
    }

    public bool CanExecute(GameState state)
    {
        if (!state.IsStarted) return false;
        if (_playerId < 0 || _playerId >= state.Players.Count) return false;
        if (state.Deck is null || state.Deck.Count == 0) return false;
        return true;
    }

    public IReadOnlyList<GameEvent> Execute(GameState state)
    {
        var deck = state.Deck!;
        var ids = new List<Guid>(deck.Count);
        for (int i = 0; i < deck.Count; i++)
        {
            ids.Add(deck[i].InstanceId);
        }

        var rng = new Random();
        for (int i = ids.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (ids[i], ids[j]) = (ids[j], ids[i]);
        }

        return new GameEvent[]
        {
            new DeckShuffled
            {
                PlayerId = _playerId,
                PostShuffleInstanceIds = ids,
            }
        };
    }
}
```

- [ ] **Step 4: Run tests; verify they pass**

```bash
dotnet test Tests~/PureCSharp/CardCore.PureTests.csproj --filter "FullyQualifiedName~CardCore.PureTests.ShuffleDeckCommandTests"
```

Expected: all 7 pass.

- [ ] **Step 5: Stage**

```bash
git add Runtime/Commands/ShuffleDeckCommand.cs Tests~/PureCSharp/ShuffleDeckCommandTests.cs
git status
```

Stop and await user commit.

---

## Task 10: Wire 4 new apply paths into `GameState` + tests

**Files:**
- Modify: `Runtime/GameState.cs`
- Modify: `Tests~/PureCSharp/GameStateTests.cs`

Four new private apply methods + four new switch cases. After this task, every command from Tasks 6–9 is end-to-end functional (the engine can execute them and the state mutates correctly).

- [ ] **Step 1: Read the current `GameStateTests.cs` to see the apply-test pattern**

```bash
cat Tests~/PureCSharp/GameStateTests.cs
```

Existing tests likely call `state.ApplyForTest(...)` directly and assert post-state. Match this style.

- [ ] **Step 2: Append failing tests to `GameStateTests.cs`**

Add these inside the existing `GameStateTests` class. If the test class doesn't already have a `NewCard` / `StartedState` helper at the top, add them (mirroring `DrawCardCommandTests.cs`):

```csharp
    [Fact]
    public void ApplyCardDiscarded_MovesCardFromHandToDiscardPile()
    {
        var card = NewCard("a");
        var s = new GameState();
        s.ApplyForTest(new GameStarted
        {
            SequenceId = 0, Timestamp = 0,
            InitialDeckOrder = new[] { card }, PlayerCount = 1, Seed = 0,
        });
        s.ApplyForTest(new CardDrawn
        {
            SequenceId = 1, Timestamp = 0,
            PlayerId = 0, InstanceId = card.InstanceId, DeckIndexBefore = 0,
        });

        s.ApplyForTest(new CardDiscarded
        {
            SequenceId = 2, Timestamp = 0,
            PlayerId = 0, InstanceId = card.InstanceId, HandIndexBefore = 0,
        });

        Assert.Equal(0, s.Players[0].Hand.Count);
        Assert.Equal(1, s.Players[0].DiscardPile.Count);
        Assert.Equal(card.InstanceId, s.Players[0].DiscardPile[0].InstanceId);
    }

    [Fact]
    public void ApplyCardDiscarded_InstanceIdMismatch_Throws()
    {
        var a = NewCard("a");
        var b = NewCard("b");
        var s = new GameState();
        s.ApplyForTest(new GameStarted
        {
            SequenceId = 0, Timestamp = 0,
            InitialDeckOrder = new[] { a, b }, PlayerCount = 1, Seed = 0,
        });
        s.ApplyForTest(new CardDrawn
        {
            SequenceId = 1, Timestamp = 0,
            PlayerId = 0, InstanceId = a.InstanceId, DeckIndexBefore = 0,
        });

        Assert.Throws<InvalidOperationException>(() => s.ApplyForTest(new CardDiscarded
        {
            SequenceId = 2, Timestamp = 0,
            PlayerId = 0, InstanceId = b.InstanceId, HandIndexBefore = 0, // wrong id at index 0
        }));
    }

    [Fact]
    public void ApplyCardDestroyed_RemovesCardFromHand_DiscardUntouched()
    {
        var card = NewCard("a");
        var s = new GameState();
        s.ApplyForTest(new GameStarted
        {
            SequenceId = 0, Timestamp = 0,
            InitialDeckOrder = new[] { card }, PlayerCount = 1, Seed = 0,
        });
        s.ApplyForTest(new CardDrawn
        {
            SequenceId = 1, Timestamp = 0,
            PlayerId = 0, InstanceId = card.InstanceId, DeckIndexBefore = 0,
        });

        s.ApplyForTest(new CardDestroyed
        {
            SequenceId = 2, Timestamp = 0,
            PlayerId = 0, InstanceId = card.InstanceId, HandIndexBefore = 0,
        });

        Assert.Equal(0, s.Players[0].Hand.Count);
        Assert.Equal(0, s.Players[0].DiscardPile.Count);
    }

    [Fact]
    public void ApplyDiscardMovedToDeck_DrainsPileIntoDeckInOrder()
    {
        // Start, draw, play (drains deck), populate discard manually.
        var seed = NewCard("seed");
        var s = new GameState();
        s.ApplyForTest(new GameStarted
        {
            SequenceId = 0, Timestamp = 0,
            InitialDeckOrder = new[] { seed }, PlayerCount = 1, Seed = 0,
        });
        s.ApplyForTest(new CardDrawn
        {
            SequenceId = 1, Timestamp = 0,
            PlayerId = 0, InstanceId = seed.InstanceId, DeckIndexBefore = 0,
        });
        s.ApplyForTest(new CardPlayed
        {
            SequenceId = 2, Timestamp = 0,
            PlayerId = 0, InstanceId = seed.InstanceId,
            HandIndexBefore = 0, PlayAreaIndexAfter = 0,
        });
        var a = NewCard("a");
        var b = NewCard("b");
        s.Players[0].DiscardPile.Add(a);
        s.Players[0].DiscardPile.Add(b);

        s.ApplyForTest(new DiscardMovedToDeck
        {
            SequenceId = 3, Timestamp = 0,
            PlayerId = 0,
            InstanceIds = new List<Guid> { a.InstanceId, b.InstanceId },
        });

        Assert.Equal(0, s.Players[0].DiscardPile.Count);
        Assert.Equal(2, s.Deck!.Count);
        Assert.Equal(a.InstanceId, s.Deck[0].InstanceId);
        Assert.Equal(b.InstanceId, s.Deck[1].InstanceId);
    }

    [Fact]
    public void ApplyDiscardMovedToDeck_DeckNotEmpty_Throws()
    {
        var seed = NewCard("seed");
        var s = new GameState();
        s.ApplyForTest(new GameStarted
        {
            SequenceId = 0, Timestamp = 0,
            InitialDeckOrder = new[] { seed }, PlayerCount = 1, Seed = 0,
        });
        // Deck still has seed.
        s.Players[0].DiscardPile.Add(NewCard("a"));

        Assert.Throws<InvalidOperationException>(() => s.ApplyForTest(new DiscardMovedToDeck
        {
            SequenceId = 1, Timestamp = 0,
            PlayerId = 0,
            InstanceIds = new List<Guid> { s.Players[0].DiscardPile[0].InstanceId },
        }));
    }

    [Fact]
    public void ApplyDiscardMovedToDeck_IdSetMismatch_Throws()
    {
        var seed = NewCard("seed");
        var s = new GameState();
        s.ApplyForTest(new GameStarted
        {
            SequenceId = 0, Timestamp = 0,
            InitialDeckOrder = new[] { seed }, PlayerCount = 1, Seed = 0,
        });
        s.ApplyForTest(new CardDrawn
        {
            SequenceId = 1, Timestamp = 0,
            PlayerId = 0, InstanceId = seed.InstanceId, DeckIndexBefore = 0,
        });
        s.ApplyForTest(new CardPlayed
        {
            SequenceId = 2, Timestamp = 0,
            PlayerId = 0, InstanceId = seed.InstanceId,
            HandIndexBefore = 0, PlayAreaIndexAfter = 0,
        });
        s.Players[0].DiscardPile.Add(NewCard("a"));

        Assert.Throws<InvalidOperationException>(() => s.ApplyForTest(new DiscardMovedToDeck
        {
            SequenceId = 3, Timestamp = 0,
            PlayerId = 0,
            InstanceIds = new List<Guid> { Guid.NewGuid() }, // wrong id
        }));
    }

    [Fact]
    public void ApplyDeckShuffled_ReordersDeckToMatchEvent()
    {
        var a = NewCard("a");
        var b = NewCard("b");
        var c = NewCard("c");
        var s = new GameState();
        s.ApplyForTest(new GameStarted
        {
            SequenceId = 0, Timestamp = 0,
            InitialDeckOrder = new[] { a, b, c }, PlayerCount = 1, Seed = 0,
        });

        s.ApplyForTest(new DeckShuffled
        {
            SequenceId = 1, Timestamp = 0,
            PlayerId = 0,
            PostShuffleInstanceIds = new List<Guid> { c.InstanceId, a.InstanceId, b.InstanceId },
        });

        Assert.Equal(c.InstanceId, s.Deck![0].InstanceId);
        Assert.Equal(a.InstanceId, s.Deck[1].InstanceId);
        Assert.Equal(b.InstanceId, s.Deck[2].InstanceId);
    }

    [Fact]
    public void ApplyDeckShuffled_LengthMismatch_Throws()
    {
        var a = NewCard("a");
        var b = NewCard("b");
        var s = new GameState();
        s.ApplyForTest(new GameStarted
        {
            SequenceId = 0, Timestamp = 0,
            InitialDeckOrder = new[] { a, b }, PlayerCount = 1, Seed = 0,
        });

        Assert.Throws<InvalidOperationException>(() => s.ApplyForTest(new DeckShuffled
        {
            SequenceId = 1, Timestamp = 0,
            PlayerId = 0,
            PostShuffleInstanceIds = new List<Guid> { a.InstanceId }, // length 1, deck has 2
        }));
    }
```

If `GameStateTests.cs` doesn't already have the required usings at top, add them:

```csharp
using System;
using System.Collections.Generic;
using CardCore.Events;
```

If there's no shared `NewCard` helper at the top of the class, add:

```csharp
    private static CardInstance NewCard(string defId = "c") =>
        CardInstance.From(new CardDefinition(defId));
```

- [ ] **Step 3: Run tests; verify they fail**

```bash
dotnet test Tests~/PureCSharp/CardCore.PureTests.csproj --filter "FullyQualifiedName~CardCore.PureTests.GameStateTests"
```

Expected: new tests fail — `GameState.Apply` throws "Unknown event type" for the four new event types.

- [ ] **Step 4: Update `Runtime/GameState.cs`**

Open `Runtime/GameState.cs`. In the `Apply` switch, add four new cases immediately above the `default:` line:

```csharp
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
```

Then add these four private methods at the bottom of the class (before the closing brace):

```csharp
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

        var pileIds = new HashSet<Guid>();
        for (int i = 0; i < pile.Count; i++) pileIds.Add(pile[i].InstanceId);
        foreach (var id in evt.InstanceIds)
        {
            if (!pileIds.Contains(id))
                throw new InvalidOperationException(
                    $"DiscardMovedToDeck id {id} not present in discard pile at SequenceId {evt.SequenceId}.");
        }

        // Build the transfer list in the event's specified order, then drain the pile.
        var transfer = new List<CardInstance>(pile.Count);
        var pileSnapshot = new List<CardInstance>(pile.Count);
        for (int i = 0; i < pile.Count; i++) pileSnapshot.Add(pile[i]);
        foreach (var id in evt.InstanceIds)
        {
            for (int i = 0; i < pileSnapshot.Count; i++)
            {
                if (pileSnapshot[i].InstanceId == id)
                {
                    transfer.Add(pileSnapshot[i]);
                    pileSnapshot.RemoveAt(i);
                    break;
                }
            }
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
```

Also ensure the file has these usings at the top (the existing `Apply` already imports `CardCore.Events`, so this is mostly a sanity check):

```csharp
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using CardCore.Events;
```

- [ ] **Step 5: Run tests; verify they pass**

```bash
dotnet test Tests~/PureCSharp/CardCore.PureTests.csproj --filter "FullyQualifiedName~CardCore.PureTests.GameStateTests"
```

Expected: all `GameStateTests` (existing + 8 new) pass.

Run the full suite:

```bash
dotnet test Tests~/PureCSharp/CardCore.PureTests.csproj
```

Expected: no regressions.

- [ ] **Step 6: Stage**

```bash
git add Runtime/GameState.cs Tests~/PureCSharp/GameStateTests.cs
git status
```

Stop and await user commit.

---

## Task 11: Peek accessors on `IGameEngine` + `GameEngine` — TDD

**Files:**
- Modify: `Runtime/IGameEngine.cs`
- Modify: `Runtime/GameEngine.cs`
- Create: `Tests~/PureCSharp/PeekAccessorsTests.cs`

Adds `int GetDeckCount(int playerId)` and `int GetDiscardCount(int playerId)` to both. These bypass `GetCurrentState`'s JSON-roundtrip clone — they're cheap reads against the live `_state`.

- [ ] **Step 1: Write the failing tests**

Create `Tests~/PureCSharp/PeekAccessorsTests.cs` with EXACTLY this content:

```csharp
using System;
using System.Collections.Generic;
using CardCore;
using CardCore.Commands;
using CardCore.Events;
using Xunit;

namespace CardCore.PureTests;

public class PeekAccessorsTests
{
    private static CardInstance NewCard(string defId = "c") =>
        CardInstance.From(new CardDefinition(defId));

    private static List<CardInstance> SmallDeck() => new()
    {
        NewCard("a"), NewCard("b"), NewCard("c"),
    };

    [Fact]
    public void GetDeckCount_PreStart_Throws()
    {
        var engine = new GameEngine();
        Assert.Throws<InvalidOperationException>(() => engine.GetDeckCount(0));
    }

    [Fact]
    public void GetDiscardCount_PreStart_Throws()
    {
        var engine = new GameEngine();
        Assert.Throws<InvalidOperationException>(() => engine.GetDiscardCount(0));
    }

    [Fact]
    public void GetDeckCount_AfterStart_ReturnsDeckSize()
    {
        var engine = new GameEngine();
        engine.ExecuteCommand(new StartGameCommand(SmallDeck(), 1, 0));
        Assert.Equal(3, engine.GetDeckCount(0));
    }

    [Fact]
    public void GetDeckCount_AfterDraws_Decrements()
    {
        var engine = new GameEngine();
        engine.ExecuteCommand(new StartGameCommand(SmallDeck(), 1, 0));
        engine.ExecuteCommand(new DrawCardCommand(0));
        Assert.Equal(2, engine.GetDeckCount(0));
    }

    [Fact]
    public void GetDiscardCount_AfterStart_IsZero()
    {
        var engine = new GameEngine();
        engine.ExecuteCommand(new StartGameCommand(SmallDeck(), 1, 0));
        Assert.Equal(0, engine.GetDiscardCount(0));
    }

    [Fact]
    public void GetDiscardCount_AfterDiscardCommand_Increments()
    {
        var engine = new GameEngine();
        engine.ExecuteCommand(new StartGameCommand(SmallDeck(), 1, 0));
        engine.ExecuteCommand(new DrawCardCommand(0));
        var card = engine.GetCurrentState().Players[0].Hand[0];
        engine.ExecuteCommand(new DiscardCommand(0, card.InstanceId));
        Assert.Equal(1, engine.GetDiscardCount(0));
    }

    [Fact]
    public void GetDeckCount_InvalidPlayerId_Throws()
    {
        var engine = new GameEngine();
        engine.ExecuteCommand(new StartGameCommand(SmallDeck(), 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.GetDeckCount(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.GetDeckCount(99));
    }

    [Fact]
    public void GetDiscardCount_InvalidPlayerId_Throws()
    {
        var engine = new GameEngine();
        engine.ExecuteCommand(new StartGameCommand(SmallDeck(), 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.GetDiscardCount(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.GetDiscardCount(99));
    }
}
```

- [ ] **Step 2: Run tests; verify they fail**

```bash
dotnet test Tests~/PureCSharp/CardCore.PureTests.csproj --filter "FullyQualifiedName~CardCore.PureTests.PeekAccessorsTests"
```

Expected: compile errors — `GetDeckCount` and `GetDiscardCount` don't exist on `GameEngine`.

- [ ] **Step 3: Update `Runtime/IGameEngine.cs`**

Replace `Runtime/IGameEngine.cs` with EXACTLY this:

```csharp
using System.Collections.Generic;
using CardCore.Commands;

namespace CardCore;

public interface IGameEngine
{
    IReadOnlyList<GameEvent> ExecuteCommand(IGameCommand command);
    IReadOnlyList<GameEvent> GetEventLog();
    GameState GetStateAtIndex(int eventIndex);
    GameState GetCurrentState();
    void LoadEventLog(IReadOnlyList<GameEvent> events);
    int GetDeckCount(int playerId);
    int GetDiscardCount(int playerId);
}
```

- [ ] **Step 4: Update `Runtime/GameEngine.cs`**

Open `Runtime/GameEngine.cs`. Add these two methods inside the `GameEngine` class (anywhere — convention is after `LoadEventLog`):

```csharp
    public int GetDeckCount(int playerId)
    {
        if (!_state.IsStarted)
            throw new InvalidOperationException("GetDeckCount requires the game to be started.");
        if (playerId < 0 || playerId >= _state.Players.Count)
            throw new ArgumentOutOfRangeException(nameof(playerId));
        return _state.Deck?.Count ?? 0;
    }

    public int GetDiscardCount(int playerId)
    {
        if (!_state.IsStarted)
            throw new InvalidOperationException("GetDiscardCount requires the game to be started.");
        if (playerId < 0 || playerId >= _state.Players.Count)
            throw new ArgumentOutOfRangeException(nameof(playerId));
        return _state.Players[playerId].DiscardPile.Count;
    }
```

- [ ] **Step 5: Run tests; verify they pass**

```bash
dotnet test Tests~/PureCSharp/CardCore.PureTests.csproj --filter "FullyQualifiedName~CardCore.PureTests.PeekAccessorsTests"
```

Expected: all 8 pass.

Run the full suite:

```bash
dotnet test Tests~/PureCSharp/CardCore.PureTests.csproj
```

Expected: no regressions.

- [ ] **Step 6: Stage**

```bash
git add Runtime/IGameEngine.cs Runtime/GameEngine.cs Tests~/PureCSharp/PeekAccessorsTests.cs
git status
```

Stop and await user commit.

---

## Task 12: End-to-end reshuffle round-trip test

**Files:**
- Create: `Tests~/PureCSharp/ReshuffleRoundTripTests.cs`

Pure integration test. Runs the full sequence (`StartGame` → drain deck → discard all → `MoveDiscardToDeck` → `ShuffleDeck` → draw again), serializes the event log via `JsonSettings`, loads it into a fresh engine, asserts both final states are byte-identical. Mirrors the existing `EventReplay_ReconstructsIdenticalState` pattern from `GameEngineTests.cs`.

- [ ] **Step 1: Write the test**

Create `Tests~/PureCSharp/ReshuffleRoundTripTests.cs` with EXACTLY this content:

```csharp
using System.Collections.Generic;
using CardCore;
using CardCore.Commands;
using Newtonsoft.Json;
using Xunit;

namespace CardCore.PureTests;

public class ReshuffleRoundTripTests
{
    private static CardInstance NewCard(string defId) =>
        CardInstance.From(new CardDefinition(defId));

    [Fact]
    public void FullReshuffleCycle_ReplaysToIdenticalState()
    {
        var deckCards = new List<CardInstance>
        {
            NewCard("a"), NewCard("b"), NewCard("c"),
        };

        var engineA = new GameEngine();
        engineA.ExecuteCommand(new StartGameCommand(deckCards, 1, 42));

        // Draw the three cards.
        engineA.ExecuteCommand(new DrawCardCommand(0));
        engineA.ExecuteCommand(new DrawCardCommand(0));
        engineA.ExecuteCommand(new DrawCardCommand(0));

        // Discard all three (by id, captured fresh each time since hand shifts).
        var stateAfterDraws = engineA.GetCurrentState();
        var idsInHand = new List<System.Guid>();
        for (int i = 0; i < stateAfterDraws.Players[0].Hand.Count; i++)
            idsInHand.Add(stateAfterDraws.Players[0].Hand[i].InstanceId);
        foreach (var id in idsInHand)
            engineA.ExecuteCommand(new DiscardCommand(0, id));

        // Deck is empty, discard has 3 → reshuffle.
        engineA.ExecuteCommand(new MoveDiscardToDeckCommand(0));
        engineA.ExecuteCommand(new ShuffleDeckCommand(0));

        // Draw one card from the reshuffled deck.
        engineA.ExecuteCommand(new DrawCardCommand(0));

        var json = JsonConvert.SerializeObject(engineA.GetEventLog(), GameEvent.JsonSettings);
        var loaded = JsonConvert.DeserializeObject<List<GameEvent>>(json, GameEvent.JsonSettings)!;

        var engineB = new GameEngine();
        engineB.LoadEventLog(loaded);

        var jsonA = JsonConvert.SerializeObject(engineA.GetCurrentState(), GameEvent.JsonSettings);
        var jsonB = JsonConvert.SerializeObject(engineB.GetCurrentState(), GameEvent.JsonSettings);
        Assert.Equal(jsonA, jsonB);

        // And the counts match expectation.
        Assert.Equal(2, engineA.GetDeckCount(0));
        Assert.Equal(0, engineA.GetDiscardCount(0));
        Assert.Equal(1, engineA.GetCurrentState().Players[0].Hand.Count);
    }
}
```

- [ ] **Step 2: Run the test; verify it passes**

```bash
dotnet test Tests~/PureCSharp/CardCore.PureTests.csproj --filter "FullyQualifiedName~CardCore.PureTests.ReshuffleRoundTripTests"
```

Expected: 1 test passes. If it fails, the regression is in one of the apply paths (Task 10) or commands (Tasks 6–9) — read the failure carefully and fix at the source. Don't change the test to match a bug.

Run the full suite for a final regression check:

```bash
dotnet test Tests~/PureCSharp/CardCore.PureTests.csproj
```

Expected: all tests green.

- [ ] **Step 3: Stage**

```bash
git add Tests~/PureCSharp/ReshuffleRoundTripTests.cs
git status
```

Stop and await user commit.

---

## Task 13: Update `Documentation~/unity-client.md`

**Files:**
- Modify: `Documentation~/unity-client.md`

Per project rule (CLAUDE.md): public API surface changes update this doc in the same change. Sections affected: `IGameEngine`, Commands, Events, GameState/Models, Calling conventions.

- [ ] **Step 1: Read the current `Documentation~/unity-client.md`**

```bash
cat Documentation~/unity-client.md
```

Locate these anchor points (line numbers from the post-`expose-ReplaceAction` version; verify by re-reading):
- The `### IGameEngine` section — currently ends with `void LoadEventLog(IReadOnlyList<GameEvent> events);` followed by its description.
- The `### CardPlayed` (event) section — last existing event section before `### GameState`.
- The `### PlayCardCommand` section — last existing command section before `### GameStarted` (event).
- The `### GameState` section — defines `Players`, `PlayArea`, `Deck`, etc.
- The `## Calling conventions` section — bullets describing how clients use the engine.

- [ ] **Step 2: Append to the `### IGameEngine` section**

After the `LoadEventLog` description block in `Documentation~/unity-client.md`, add:

```markdown
```csharp
int GetDeckCount(int playerId);
```
Returns the current deck size. Reads `_state` directly — does not clone. Throws `InvalidOperationException` if the game is not started, `ArgumentOutOfRangeException` if `playerId` is out of range.
Use: `if (engine.GetDeckCount(0) == 0) { /* trigger reshuffle policy */ }`

```csharp
int GetDiscardCount(int playerId);
```
Returns the current size of `playerId`'s discard pile. Same non-cloning read semantics and throw contract as `GetDeckCount`.
Use: `int pile = engine.GetDiscardCount(0);`
```

- [ ] **Step 3: Add new command sections**

Immediately after the existing `### PlayCardCommand` section in `Documentation~/unity-client.md`, add:

```markdown
### `DiscardCommand`

```csharp
public DiscardCommand(int playerId, Guid instanceId);
```
Moves the card identified by `instanceId` from `playerId`'s hand to that player's discard pile. `CanExecute` is false if the game isn't started, `playerId` is invalid, or no card with `instanceId` is in the hand. Emits one `CardDiscarded`.
Use: `engine.ExecuteCommand(new DiscardCommand(0, cardInstanceId));`

### `DestroyCardCommand`

```csharp
public DestroyCardCommand(int playerId, Guid instanceId);
```
Removes the card identified by `instanceId` from `playerId`'s hand entirely — it does not enter the discard pile. Same `CanExecute` rules as `DiscardCommand`. Emits one `CardDestroyed`.
Use: `engine.ExecuteCommand(new DestroyCardCommand(0, cardInstanceId));`

### `MoveDiscardToDeckCommand`

```csharp
public MoveDiscardToDeckCommand(int playerId);
```
Empties `playerId`'s discard pile back into the shared deck, preserving discard pile order. `CanExecute` requires the deck to be empty and the discard pile to be non-empty (the engine enforces this so direct callers can't bypass the policy). Emits one `DiscardMovedToDeck`. Always followed by `ShuffleDeckCommand` in the client's reshuffle policy.
Use: `engine.ExecuteCommand(new MoveDiscardToDeckCommand(0));`

### `ShuffleDeckCommand`

```csharp
public ShuffleDeckCommand(int playerId);
```
Reshuffles the deck. The event records the post-shuffle order so replay is deterministic — the command itself uses an unseeded `System.Random`. `CanExecute` requires the deck to be non-empty. Emits one `DeckShuffled`.
Use: `engine.ExecuteCommand(new ShuffleDeckCommand(0));`
```

- [ ] **Step 4: Add new event sections**

Immediately after the existing `### CardPlayed` (event) section, add:

```markdown
### `CardDiscarded` (event)

```csharp
public sealed record CardDiscarded : GameEvent
{
    public int PlayerId { get; init; }
    public Guid InstanceId { get; init; }
    public int HandIndexBefore { get; init; }
}
```
Records that the card with `InstanceId` moved from `playerId`'s hand position `HandIndexBefore` to that player's discard pile.

### `CardDestroyed` (event)

```csharp
public sealed record CardDestroyed : GameEvent
{
    public int PlayerId { get; init; }
    public Guid InstanceId { get; init; }
    public int HandIndexBefore { get; init; }
}
```
Records that the card with `InstanceId` was removed from `playerId`'s hand position `HandIndexBefore` and ceased to exist (not transferred to any pile).

### `DiscardMovedToDeck` (event)

```csharp
public sealed record DiscardMovedToDeck : GameEvent
{
    public int PlayerId { get; init; }
    public IReadOnlyList<Guid> InstanceIds { get; init; }
}
```
Records that `playerId`'s discard pile (in `InstanceIds` order) was drained into the shared deck. The engine validates that the supplied ids match the pile contents exactly.

### `DeckShuffled` (event)

```csharp
public sealed record DeckShuffled : GameEvent
{
    public int PlayerId { get; init; }
    public IReadOnlyList<Guid> PostShuffleInstanceIds { get; init; }
}
```
Records the post-shuffle order of the deck. Replay reorders the existing deck to match — the event is the source of truth for the shuffle outcome.
```

- [ ] **Step 5: Update the `### GameState` section to mention `DiscardPile`**

In the `### GameState` section, after the `IReadOnlyList<Player> Players { get; }` line in the block of read-only properties, no change is needed — `Players` is already listed. **Add a sentence after the description**:

Locate the existing description after the property block:

```
All read-only. `GameState` instances handed to a client are clones — safe to read, mutations have no effect on the engine.
```

Add a new paragraph immediately after it:

```markdown
Each `Player` now exposes a `DiscardPile DiscardPile { get; }` in addition to `Hand`. The discard pile is the destination of `CardDiscarded` events and the source for `DiscardMovedToDeck`. Pre-C.3 saved logs (without a `DiscardPile` in the JSON) rehydrate with an empty pile.
```

- [ ] **Step 6: Add a `### DiscardPile` model section**

After the `### CardInstance` section (or wherever models are documented; look for the existing pattern), add:

```markdown
### `DiscardPile`

```csharp
public sealed class DiscardPile
{
    public int Count { get; }
    public IReadOnlyList<CardInstance> Cards { get; }
    public CardInstance this[int index] { get; }
    public void Add(CardInstance card);
    public CardInstance RemoveAt(int index);
    public void AddRange(IReadOnlyList<CardInstance> cards);
}
```
Per-player pile, parallel to `Hand`. Lives on `Player.DiscardPile`. Engine code populates it via `ApplyCardDiscarded` and drains it via `ApplyDiscardMovedToDeck`; client code reads it through `GameState.Players[i].DiscardPile`.
```

- [ ] **Step 7: Append a reshuffle note to `## Calling conventions`**

At the end of the `## Calling conventions` bulleted list, add:

```markdown
- **Reshuffle is client-orchestrated.** The engine refuses to draw from an empty deck (`DrawCardCommand.CanExecute` returns false). To reshuffle, the client issues `MoveDiscardToDeckCommand` followed by `ShuffleDeckCommand`, then retries the draw. The engine intentionally does not bundle these or auto-reshuffle on draw — policy lives in the client.
```

- [ ] **Step 8: Verify the doc still reads cleanly**

```bash
head -300 Documentation~/unity-client.md
```

Check: new sections appear in plausible places, no broken markdown (unmatched code fences, missing headings).

- [ ] **Step 9: Stage**

```bash
git add Documentation~/unity-client.md
git status
```

Stop and await user commit.

---

## Task 14: Final regression sweep + cleanup

**Files:** none (verification + housekeeping)

- [ ] **Step 1: Run the full test suite**

```bash
dotnet test Tests~/PureCSharp/CardCore.PureTests.csproj
```

Expected count: baseline + new tests, all green. The new tests (per task):
- Task 1: 9 (DiscardPileTests)
- Task 2: 3 (PlayerTests additions)
- Task 3: 6 (DeckTests additions)
- Task 5: 4 (GameEventTests additions)
- Task 6: 7 (DiscardCommandTests)
- Task 7: 7 (DestroyCardCommandTests)
- Task 8: 7 (MoveDiscardToDeckCommandTests)
- Task 9: 7 (ShuffleDeckCommandTests)
- Task 10: 8 (GameStateTests additions)
- Task 11: 8 (PeekAccessorsTests)
- Task 12: 1 (ReshuffleRoundTripTests)

**Total new: 67 tests.** Plus all pre-existing tests still green.

- [ ] **Step 2: Confirm the Demo project still builds**

```bash
dotnet build Tests~/Demo/CardCore.Demo.csproj
```

Expected: builds cleanly. If the Demo project exercises the public API, it must still compile against the additive changes (it should — we added nothing breaking).

- [ ] **Step 3: Confirm no stray TODOs**

```bash
grep -rn "TODO\|FIXME" Runtime/ Tests~/PureCSharp/
```

Expected: any pre-existing TODOs unrelated to this work are fine; no new ones introduced by this branch.

- [ ] **Step 4: Confirm the unity-client.md doc references all new types**

```bash
grep -c "DiscardCommand\|DestroyCardCommand\|MoveDiscardToDeckCommand\|ShuffleDeckCommand\|CardDiscarded\|CardDestroyed\|DiscardMovedToDeck\|DeckShuffled\|DiscardPile\|GetDeckCount\|GetDiscardCount" Documentation~/unity-client.md
```

Expected: every new symbol appears at least once. If any returns 0 matches, you missed a doc section.

- [ ] **Step 5: Hand off**

```bash
git status
git log --oneline main..HEAD
```

Expected: commits per task (whatever the user committed), branch ready for PR.

Hand off to user for review and PR creation. The user opens the PR themselves and decides on the merge SHA, which the Unity-side C.3 plan will then pin in Task 0 of that plan.

---

## Self-Review

**1. Spec coverage:**

| Spec section | Task |
| --- | --- |
| `DiscardPile` model | Task 1 |
| `Player.DiscardPile` field + JSON migration | Task 2 |
| `Deck.AddRange` and `Deck.ReorderTo` internal methods | Task 3 |
| New event records (4) | Task 4 |
| `GameEventConverter` polymorphic switch entries (4) | Task 5 |
| `DiscardCommand` | Task 6 |
| `DestroyCardCommand` | Task 7 |
| `MoveDiscardToDeckCommand` | Task 8 |
| `ShuffleDeckCommand` | Task 9 |
| `GameState.Apply` switch cases + 4 private apply methods | Task 10 |
| `IGameEngine.GetDeckCount` / `GetDiscardCount` | Task 11 |
| `GameEngine` peek accessor implementations | Task 11 |
| Reshuffle round-trip integration test | Task 12 |
| `Documentation~/unity-client.md` API surface delta | Task 13 |
| Corruption-throws cases (id mismatch, length mismatch, deck-not-empty) | Task 10 |
| Probabilistic shuffle test ("not all 5 identical") | Task 9 |
| Pre-C.3 saved log compatibility (null DiscardPile in JSON) | Task 2 |

All spec requirements covered.

**2. Placeholder scan:** No "TBD", "TODO", "fill in later", "similar to Task N", or vague error-handling steps. Every code step contains the actual code; every command step contains the actual command and expected output.

**3. Type consistency check:**
- `DiscardPile` ctor signatures consistent between Task 1 (definition) and Task 2 (consumer in `Player`).
- `Player(int id, Hand hand, DiscardPile? discardPile)` ctor introduced in Task 2 matches the test signature in Task 2 Step 2 and the JSON test setup in Task 2 Step 2.
- `Deck.AddRange(IReadOnlyList<CardInstance>)` and `Deck.ReorderTo(IReadOnlyList<Guid>)` consistent between Task 3 (definition) and Task 10 (call sites in apply paths).
- Event record field names consistent between Task 4 (definitions), Task 5 (round-trip tests), Task 8/9 (command emit sites), and Task 10 (apply sites): `CardDiscarded.HandIndexBefore`, `CardDestroyed.HandIndexBefore`, `DiscardMovedToDeck.InstanceIds`, `DeckShuffled.PostShuffleInstanceIds`.
- Command constructor signatures consistent between definition (Tasks 6–9) and consumer tests (Tasks 11, 12).
- `IGameEngine.GetDeckCount(int)` / `GetDiscardCount(int)` signatures consistent between interface (Task 11 Step 3), implementation (Task 11 Step 4), tests (Task 11 Step 1), and doc (Task 13 Step 2).

**4. Ambiguity check:** `MoveDiscardToDeckCommand.Execute`'s event payload (`InstanceIds` list) is the pile's contents *in current pile order*, made explicit in Task 8 Step 3's comment block and verified in `Execute_EmitsDiscardMovedToDeck_WithIdsInPileOrder`. `ApplyDiscardMovedToDeck` re-orders the transfer to match the event's id sequence (Task 10's `transfer` list), so the engine round-trips the order exactly. Not ambiguous.

---

## Out of scope (per spec)

- Per-player decks (shared `Deck` stays; `playerId` parameter present on commands for future-proofing only).
- End-of-turn / skip-fate logic (client-side `GameRules`).
- Per-player turn counter and client-side C# events (`OnTurnEnded`, `OnDrawFailed` etc.).
- Discard-from-play-area, discard-from-deck commands.
- Metadata bag on `CardDefinition` (`skipFate` stays client metadata).
- Win/lose condition events.
- `Hand → DiscardPile` migration of existing data (no existing logs have discard piles).
