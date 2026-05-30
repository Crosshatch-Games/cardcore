# CardCore for Unity

> Headless event-sourced card game engine. This doc covers the minimum to call CardCore from a Unity scene. For visualizer / scrubber / event-replay UI patterns, see `claude.md` at the repo root.

## Install

**Requirements:** Unity 6.3 LTS or newer.

In Unity: open **Window → Package Manager → ＋ → Add package from git URL…** and paste:

```
https://github.com/Crosshatch-Games/cardcore.git
```

Unity will fetch the package and import it. The `Runtime/` folder becomes the `CardCore` assembly; tests under `Tests/Runtime/` are not imported by default.

## 30-second hello world

Create `Assets/Scripts/CardCoreDemo.cs`, attach it to any GameObject, press Play. Console shows the deck shrinking and a card moving to the play area.

```csharp
using System.Collections.Generic;
using CardCore;
using CardCore.Commands;
using UnityEngine;

public sealed class CardCoreDemo : MonoBehaviour
{
    private void Start()
    {
        var copper = new CardDefinition("copper");
        var silver = new CardDefinition("silver");
        var gold = new CardDefinition("gold");

        var deck = new List<CardInstance>
        {
            CardInstance.From(copper),
            CardInstance.From(silver),
            CardInstance.From(gold),
        };

        var engine = new GameEngine();
        engine.ExecuteCommand(new StartGameCommand(deck, playerCount: 1, seed: 42));
        engine.ExecuteCommand(new DrawCardCommand(playerId: 0));
        engine.ExecuteCommand(new PlayCardCommand(playerId: 0, handIndex: 0));

        var state = engine.GetCurrentState();
        Debug.Log($"Hand: {state.Players[0].Hand.Count}  PlayArea: {state.PlayArea.Count}  Deck: {state.Deck!.Count}");
    }
}
```

Expected console output: `Hand: 0  PlayArea: 1  Deck: 2`.

## Public API surface

Every public type a Unity client needs. Format: signature + behavior + use site.

### `IGameEngine`

```csharp
IReadOnlyList<GameEvent> ExecuteCommand(IGameCommand command);
```
Validates `command.CanExecute(state)`, executes, stamps `SequenceId`/`Timestamp`, appends to log, applies to state. Throws `InvalidOperationException` if `CanExecute` is false.
Use: `engine.ExecuteCommand(new DrawCardCommand(playerId: 0));`

```csharp
IReadOnlyList<GameEvent> GetEventLog();
```
Returns the full event log as a read-only view. Persist this for save/load.
Use: `var log = engine.GetEventLog();`

```csharp
GameState GetStateAtIndex(int eventIndex);
```
Replays events `[0..eventIndex]` into a fresh state and returns a clone. Throws `ArgumentOutOfRangeException` if out of range.
Use: `var snapshot = engine.GetStateAtIndex(5);`

```csharp
GameState GetCurrentState();
```
Equivalent to `GetStateAtIndex(log.Count - 1)`. Returns a clone — safe to read, modifications don't affect the engine.
Use: `var state = engine.GetCurrentState();`

```csharp
void LoadEventLog(IReadOnlyList<GameEvent> events);
```
Replays a saved log into a fresh engine. Throws if engine already has events, or if the log is malformed.
Use: `engine.LoadEventLog(deserializedEvents);`

```csharp
int GetDeckCount(int playerId);
```
Returns the current deck size. Reads `_state` directly — does not clone. Throws `InvalidOperationException` if the game is not started, `ArgumentOutOfRangeException` if `playerId` is out of range. Cheap enough to call in tight loops (e.g. a `TryDraw` reshuffle gate).
Use: `if (engine.GetDeckCount(0) == 0) { /* trigger reshuffle policy */ }`

```csharp
int GetDiscardCount(int playerId);
```
Returns the current size of `playerId`'s discard pile. Same non-cloning read semantics and throw contract as `GetDeckCount`.
Use: `int pile = engine.GetDiscardCount(0);`

### `GameEngine`

```csharp
public GameEngine();
```
Empty engine. The first command must be `StartGameCommand`.

### `StartGameCommand`

```csharp
public StartGameCommand(IReadOnlyList<CardInstance> deck, int playerCount, int seed);
```
Validates non-null/non-empty deck, no duplicate `CardInstance.InstanceId`s, `playerCount >= 1`. Emits one `GameStarted` event carrying the post-shuffle deck order.
Use: `engine.ExecuteCommand(new StartGameCommand(deck, playerCount: 2, seed: 42));`

### `DrawCardCommand`

```csharp
public DrawCardCommand(int playerId);
```
Top-of-deck → player's hand. `CanExecute` is false if game not started, deck empty, or invalid `playerId`. Emits one `CardDrawn`.
Use: `engine.ExecuteCommand(new DrawCardCommand(playerId: 0));`

### `PlayCardCommand`

```csharp
public PlayCardCommand(int playerId, int handIndex);
```
Player's hand card at `handIndex` → play area. `CanExecute` is false if game not started or indices invalid. Emits one `CardPlayed`.
Use: `engine.ExecuteCommand(new PlayCardCommand(playerId: 0, handIndex: 0));`

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

### `GameStarted` (event)

```csharp
public sealed record GameStarted : GameEvent
{
    public IReadOnlyList<CardInstance> InitialDeckOrder { get; init; }
    public int PlayerCount { get; init; }
    public int Seed { get; init; }
}
```
Carries the post-shuffle deck order (replay does not re-run `System.Random`). Always the first event.

### `CardDrawn` (event)

```csharp
public sealed record CardDrawn : GameEvent
{
    public int PlayerId { get; init; }
    public Guid InstanceId { get; init; }
    public int DeckIndexBefore { get; init; }
}
```
Records that the card with `InstanceId` moved from `DeckIndexBefore` to `PlayerId`'s hand.

### `CardPlayed` (event)

```csharp
public sealed record CardPlayed : GameEvent
{
    public int PlayerId { get; init; }
    public Guid InstanceId { get; init; }
    public int HandIndexBefore { get; init; }
    public int PlayAreaIndexAfter { get; init; }
}
```
Records that the card with `InstanceId` moved from hand position `HandIndexBefore` to play-area position `PlayAreaIndexAfter`.

### `CardDiscarded` (event)

```csharp
public sealed record CardDiscarded : GameEvent
{
    public int PlayerId { get; init; }
    public Guid InstanceId { get; init; }
    public int HandIndexBefore { get; init; }
}
```
Records that the card with `InstanceId` moved from `PlayerId`'s hand position `HandIndexBefore` to that player's discard pile.

### `CardDestroyed` (event)

```csharp
public sealed record CardDestroyed : GameEvent
{
    public int PlayerId { get; init; }
    public Guid InstanceId { get; init; }
    public int HandIndexBefore { get; init; }
}
```
Records that the card with `InstanceId` was removed from `PlayerId`'s hand position `HandIndexBefore` and ceased to exist (not transferred to any pile).

### `DiscardMovedToDeck` (event)

```csharp
public sealed record DiscardMovedToDeck : GameEvent
{
    public int PlayerId { get; init; }
    public IReadOnlyList<Guid> InstanceIds { get; init; }
}
```
Records that `PlayerId`'s discard pile (in `InstanceIds` order) was drained into the shared deck. The engine validates that the supplied ids match the pile contents exactly.

### `DeckShuffled` (event)

```csharp
public sealed record DeckShuffled : GameEvent
{
    public int PlayerId { get; init; }
    public IReadOnlyList<Guid> PostShuffleInstanceIds { get; init; }
}
```
Records the post-shuffle order of the deck. Replay reorders the existing deck to match — the event is the source of truth for the shuffle outcome.

### `GameState`

```csharp
public IReadOnlyList<Player> Players { get; }
public IReadOnlyList<CardInstance> PlayArea { get; }
public Deck? Deck { get; }
public int Seed { get; }
public bool IsStarted { get; }
```
All read-only. `GameState` instances handed to a client are clones — safe to read, mutations have no effect on the engine.

Each `Player` exposes `DiscardPile DiscardPile { get; }` in addition to `Hand`. The discard pile is the destination of `CardDiscarded` events and the source for `DiscardMovedToDeck`. Pre-C.3 saved logs (without a `DiscardPile` in the JSON) rehydrate with an empty pile.

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

### `CardDefinition`

```csharp
public sealed record CardDefinition
{
    public string Id { get; }                                    // ONLY required field; lowercase, no whitespace
    public MarkdownText Name { get; }
    public IReadOnlyList<string> Types { get; }
    public IReadOnlyList<CurrencyAmount> Costs { get; }
    public IReadOnlyList<CurrencyAmount> Rewards { get; }
    public IReadOnlyList<CurrencyAmount> Thresholds { get; }
    public IReadOnlyList<Action> Actions { get; }
    public IReadOnlyList<MarkdownText> Targets { get; }
    public string? Back { get; }
    public string? Rarity { get; }
    public MarkdownText Flavor { get; }
}
```
Immutable card content, loaded from JSON via `CardCatalogLoader`. Lives in a `CardCatalog` for the lifetime of a game. Only `Id` is required; every other field defaults to empty/null when missing in JSON.


  ### `CardInstance`

  ```csharp
  public sealed class CardInstance
  {
      public Guid InstanceId { get; }                  // unique per instance, runtime-generated
      public string DefinitionId { get; }              // points back to catalog
      public MarkdownText Name { get; }
      public IReadOnlyList<string> Types { get; }
      public IReadOnlyList<CurrencyAmount> Costs { get; }
      public IReadOnlyList<CurrencyAmount> Rewards { get; }
      public IReadOnlyList<CurrencyAmount> Thresholds { get; }
      public IReadOnlyList<Action> Actions { get; }
      public IReadOnlyList<MarkdownText> Targets { get; }
      public string? Back { get; }
      public string? Rarity { get; }
      public MarkdownText Flavor { get; }

      public static CardInstance From(CardDefinition def);

      public void ReplaceAction(int index, Action action);
  }
  Mutable in-game card. Construct via CardInstance.From(definition); that's the only public construction path.

  ReplaceAction(index, action) lets a ruleset freeze a play-time decision into the card's action payload before the card is played. The canonical use case: a card whose play involves a player-chosen parameter (placement
  position, "choose one of two" branch, RNG result). The ruleset captures the choice in action.Payload, calls ReplaceAction to bake it into the card instance, then submits PlayCardCommand. The mutated card lands in
  GameState.PlayArea; the choice is now part of the event-sourced state and replay is deterministic. Throws ArgumentNullException if action is null, ArgumentOutOfRangeException if index is out of range.

  The other mutation methods (AddAction, RemoveAction, SetCost) remain internal — they're available to the (future) ruleset assembly via [InternalsVisibleTo]. JSON round-trips cleanly through the event log.
  
### `CurrencyAmount`

```csharp
public readonly record struct CurrencyAmount(int Amount, string Type);
```
Throws `ArgumentException` if `Type` is null/whitespace. `Amount` may be zero or negative — rulesets decide what those mean.

### `Action`

```csharp
public sealed record Action
{
    public string Verb { get; }
    public Newtonsoft.Json.Linq.JObject Payload { get; }
}
```
Opaque to CardCore: `Verb` is non-empty, `Payload` is any JSON object (including `{}`). Rulesets register `IActionHandler` instances per verb on an `ActionDispatcher` they own.

### `MarkdownText`

```csharp
public sealed record MarkdownText
{
    public static readonly MarkdownText Empty;
    public string Raw { get; }
    public IReadOnlyList<MarkdownToken> Tokens { get; }
}
```
Carries both the raw string and the parsed token stream. Tokens are derived from `Raw` and re-parsed on JSON deserialization.

### `MarkdownToken` and subtypes

```csharp
public abstract record MarkdownToken;
public sealed record LiteralToken(string Text) : MarkdownToken;
public sealed record IconToken(string Id) : MarkdownToken;
public sealed record KeywordToken(string Id, string? Param) : MarkdownToken;
public sealed record VariableToken(string Name) : MarkdownToken;
public sealed record TypeRefToken(string Category, string Value) : MarkdownToken;
```
The Cardcore Markdown grammar: `[id]` → `IconToken`, `#id` or `#id(param)` → `KeywordToken`, `${name}` → `VariableToken`, anything else → `LiteralToken`. `TypeRefToken` is reserved for ruleset use in structured fields.

### `MarkdownParser`

```csharp
namespace CardCore.Markdown;

public static class MarkdownParser
{
    public static MarkdownText Parse(string raw);
    public static bool TryParse(string raw, out MarkdownText result, out string? error);
}
```
Pure static. `Parse` throws `FormatException` on unbalanced brackets or unclosed variables; `TryParse` returns `false`/`error` instead.

### `CardCatalog`

```csharp
namespace CardCore.Catalog;

public sealed class CardCatalog
{
    public CardCatalog(IEnumerable<CardDefinition> definitions);
    public CardCatalog(IEnumerable<CardDefinition> definitions, IReadOnlyList<string> loadWarnings);

    public int Count { get; }
    public IReadOnlyCollection<CardDefinition> Definitions { get; }
    public IReadOnlyList<string> LoadWarnings { get; }
    public CardDefinition Get(string id);            // KeyNotFoundException on miss
    public bool TryGet(string id, out CardDefinition? def);
    public bool Contains(string id);
}
```
Throws `ArgumentException` on duplicate ids. `LoadWarnings` is populated by `CardCatalogLoader`; the single-arg constructor leaves it empty.

### `CardCatalogLoader`

```csharp
namespace CardCore.Catalog;

public static class CardCatalogLoader
{
    public static CardCatalog LoadFromDirectory(string directoryPath);
    public static CardCatalog LoadFromJson(string json);
    public static CardCatalog LoadFromStream(Stream stream);
    public static CardDefinition LoadDefinition(JObject json);
}
```
All three top-level methods return a fully-validated `CardCatalog`. If any card fails validation, the load fails with `CardCatalogLoadException` listing every failing card. Directories may contain one definition per file or arrays per file. Warnings (e.g. unpaired cost amount/type) accumulate on `CardCatalog.LoadWarnings` without halting the load.

### `IRuleset`

```csharp
namespace CardCore;

public interface IRuleset
{
    // Empty marker. The first concrete ruleset will drive what methods get added here.
}
```

### `IActionHandler`

```csharp
namespace CardCore;

public interface IActionHandler
{
    string Verb { get; }
    IReadOnlyList<GameEvent> Handle(Action action, CardInstance card, GameState state);
}
```
Implement one per verb. `Handle` returns the events to append to the log.

### `ActionDispatcher`

```csharp
namespace CardCore;

public sealed class ActionDispatcher
{
    public ActionDispatcher();
    public void Register(IActionHandler handler);   // throws InvalidOperationException on duplicate verb
    public bool IsRegistered(string verb);
    public IReadOnlyList<GameEvent> Dispatch(Action action, CardInstance card, GameState state);
    // Dispatch throws InvalidOperationException if no handler is registered for action.Verb.
}
```
Owned by the ruleset, not by `GameEngine`. The engine stays ignorant of action semantics.

## Calling conventions

The rules a client must follow:

- **Commands carry their data via the constructor.** Build a fresh command per `ExecuteCommand` call. Don't reuse-and-mutate.
- **`ExecuteCommand` throws `InvalidOperationException` when `CanExecute` returns false.** Call `command.CanExecute(state)` first if you want a non-throwing path.
- **`GetCurrentState()` and `GetStateAtIndex(n)` return cloned `GameState` objects.** Modifying the returned state has no effect on the engine — the clone is yours to read or even mutate locally.
- **The event log is the source of truth.** Persist `engine.GetEventLog()`, never the `GameState` directly. State is always derivable from the log.
- **Reshuffle is client-orchestrated.** The engine refuses to draw from an empty deck (`DrawCardCommand.CanExecute` returns false). To reshuffle, the client issues `MoveDiscardToDeckCommand` followed by `ShuffleDeckCommand`, then retries the draw. The engine intentionally does not bundle these or auto-reshuffle on draw — policy lives in the client.

## Persistence

Save = serialize the event log. Load = deserialize and feed to a fresh engine. Always pass `GameEvent.JsonSettings` so polymorphism resolves correctly.

```csharp
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using CardCore;

// Save
var json = JsonConvert.SerializeObject(engine.GetEventLog(), GameEvent.JsonSettings);
File.WriteAllText(path, json);

// Load
var loadedJson = File.ReadAllText(path);
var events = JsonConvert.DeserializeObject<List<GameEvent>>(loadedJson, GameEvent.JsonSettings)!;
var engine = new GameEngine();
engine.LoadEventLog(events);
```

`LoadEventLog` requires a fresh engine; it throws if the engine already has events.

## Adding card data

CardCore loads card content from JSON via `CardCatalogLoader`. Designers author cards as JSON files (one card per file or arrays per file) under any directory you choose; the host loads the catalog at startup.

```csharp
using System.Collections.Generic;
using CardCore;
using CardCore.Catalog;
using CardCore.Commands;

var catalog = CardCatalogLoader.LoadFromDirectory("Cards/");

var deck = new List<CardInstance>
{
    CardInstance.From(catalog.Get("copper")),
    CardInstance.From(catalog.Get("copper")),
    CardInstance.From(catalog.Get("silver")),
    CardInstance.From(catalog.Get("gold")),
};

engine.ExecuteCommand(new StartGameCommand(deck, playerCount: 2, seed: 42));
```

A minimal valid card JSON file is just:

```json
{ "id": "copper" }
```

Every other field (name, types, costs, rewards, thresholds, actions, targets, back, rarity, flavor) is optional and defaults to empty/null. Text fields use Cardcore Markdown — `[icon_id]`, `#keyword`, `#keyword(param)`, `${variable}`. See `Documentation~/Claude MD - Cardcore Cards.md` for the full grammar.

The host owns deck composition: which definitions, how many copies, in what order. CardCore does not impose a deck-building model — that's a ruleset concern.

## What this doc does NOT cover

Out of scope for "simple Unity client":

- Visualizers (`IEventVisualizer` per event type)
- Scrubber / `GameEventPlayer` MonoBehaviour
- Async event-replay UI
- Animation, prefab pooling, 3D rendering
- Unity-side testing patterns
- Concrete rulesets (action handlers, win conditions, scoring)

For visualizer / scrubber / event-replay UI patterns, see `claude.md` at the repo root.

## Troubleshooting

### `InvalidOperationException: Command X failed CanExecute against current state.`

Cause: the command can't run against the engine's current state (e.g. drawing from an empty deck, playing from an empty hand). Fix: call `command.CanExecute(engine.GetCurrentState())` first; if false, inspect the state for the missing precondition.

### `CardCatalogLoadException: Card catalog load failed: ...`

Cause: at least one card in the catalog failed validation (missing/uppercase/whitespace `id`, action with empty verb, currency with empty type, malformed markdown). The exception's `Errors` collection lists every failing card and its source. Fix the offending JSON; the load is all-or-nothing.

### Deserialized event has the wrong runtime type

Cause: deserializing without `GameEvent.JsonSettings`, or as a concrete subtype, bypasses the polymorphism discriminator. Always declare the base type AND pass the settings:

```csharp
// Wrong — no settings means the converter isn't registered; the $type discriminator is ignored
var bad = JsonConvert.DeserializeObject<GameEvent>(json);

// Right — settings register the converter that reads $type and picks the subtype
var oneEvent = JsonConvert.DeserializeObject<GameEvent>(json, GameEvent.JsonSettings);
var log = JsonConvert.DeserializeObject<List<GameEvent>>(json, GameEvent.JsonSettings);
```

### .NET version mismatch

CardCore targets `net9.0`. Unity 6.3 LTS supports `net9.0` natively; older Unity versions do not. If you see "Could not load type 'System.Collections.Generic.IReadOnlyList`1'" or similar BCL errors, you're on an older Unity.
