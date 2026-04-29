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
        var deck = new List<Card>
        {
            new Card(1, "Copper"),
            new Card(2, "Silver"),
            new Card(3, "Gold"),
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

### `GameEngine`

```csharp
public GameEngine();
```
Empty engine. The first command must be `StartGameCommand`.

### `StartGameCommand`

```csharp
public StartGameCommand(IReadOnlyList<Card> deck, int playerCount, int seed);
```
Validates non-null/non-empty deck, no duplicate `Card.Id`s, `playerCount >= 1`. Emits one `GameStarted` event carrying the post-shuffle deck order.
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

### `GameStarted` (event)

```csharp
public sealed record GameStarted : GameEvent
{
    public IReadOnlyList<Card> InitialDeckOrder { get; init; }
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
    public int CardId { get; init; }
    public int DeckIndexBefore { get; init; }
}
```
Records that `CardId` moved from `DeckIndexBefore` to `PlayerId`'s hand.

### `CardPlayed` (event)

```csharp
public sealed record CardPlayed : GameEvent
{
    public int PlayerId { get; init; }
    public int CardId { get; init; }
    public int HandIndexBefore { get; init; }
    public int PlayAreaIndexAfter { get; init; }
}
```
Records that `CardId` moved from hand position `HandIndexBefore` to play-area position `PlayAreaIndexAfter`.

### `GameState`

```csharp
public IReadOnlyList<Player> Players { get; }
public IReadOnlyList<Card> PlayArea { get; }
public Deck? Deck { get; }
public int Seed { get; }
public bool IsStarted { get; }
```
All read-only. `GameState` instances handed to a client are clones — safe to read, mutations have no effect on the engine.

### `Card`

```csharp
public sealed record Card(int Id, string Name);
```
Throws `ArgumentException` if `Id < 0` or `Name` is null/empty. Cards in this slice are intentionally minimal; richer card data is a future engine slice.

## Calling conventions

The four rules a client must follow:

- **Commands carry their data via the constructor.** Build a fresh command per `ExecuteCommand` call. Don't reuse-and-mutate.
- **`ExecuteCommand` throws `InvalidOperationException` when `CanExecute` returns false.** Call `command.CanExecute(state)` first if you want a non-throwing path.
- **`GetCurrentState()` and `GetStateAtIndex(n)` return cloned `GameState` objects.** Modifying the returned state has no effect on the engine — the clone is yours to read or even mutate locally.
- **The event log is the source of truth.** Persist `engine.GetEventLog()`, never the `GameState` directly. State is always derivable from the log.

## Persistence

Save = serialize the event log. Load = deserialize and feed to a fresh engine.

```csharp
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CardCore;

// Save
var json = JsonSerializer.Serialize(engine.GetEventLog());
File.WriteAllText(path, json);

// Load
var loadedJson = File.ReadAllText(path);
var events = JsonSerializer.Deserialize<List<GameEvent>>(loadedJson)!;
var engine = new GameEngine();
engine.LoadEventLog(events);
```

`LoadEventLog` requires a fresh engine; it throws if the engine already has events.

## Adding card data

Define the deck for a prototype as a `List<Card>` and pass it to `StartGameCommand`:

```csharp
var deck = new List<Card>
{
    new Card(1, "Copper"),
    new Card(2, "Silver"),
    new Card(3, "Gold"),
};
engine.ExecuteCommand(new StartGameCommand(deck, playerCount: 2, seed: 42));
```

`Card` is intentionally minimal in this slice — `Id` and `Name` only. Richer card data (effects, costs, types) is a future engine slice and will arrive without breaking this API.

## What this doc does NOT cover

Out of scope for "simple Unity client":

- Visualizers (`IEventVisualizer` per event type)
- Scrubber / `GameEventPlayer` MonoBehaviour
- Async event-replay UI
- Animation, prefab pooling, 3D rendering
- Unity-side testing patterns

For visualizer / scrubber / event-replay UI patterns, see `claude.md` at the repo root.

## Troubleshooting

### `InvalidOperationException: Command X failed CanExecute against current state.`

Cause: the command can't run against the engine's current state (e.g. drawing from an empty deck, playing from an empty hand). Fix: call `command.CanExecute(engine.GetCurrentState())` first; if false, inspect the state for the missing precondition.

### Deserialized event has the wrong runtime type

Cause: deserializing as a concrete subtype bypasses the polymorphism discriminator. Always declare the base type:

```csharp
// Wrong — loses the discriminator, breaks for any non-CardDrawn event in the log
var bad = JsonSerializer.Deserialize<CardDrawn>(json);

// Right — discriminator picks the correct subtype
var oneEvent = JsonSerializer.Deserialize<GameEvent>(json);
var log = JsonSerializer.Deserialize<List<GameEvent>>(json);
```

### .NET version mismatch

CardCore targets `net9.0`. Unity 6.3 LTS supports `net9.0` natively; older Unity versions do not. If you see "Could not load type 'System.Collections.Generic.IReadOnlyList`1'" or similar BCL errors, you're on an older Unity.
