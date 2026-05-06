# CardCore Walking Skeleton — Design Spec

**Date:** 2026-04-27
**Status:** Approved
**Slice:** First buildable slice of CardCore (the "walking skeleton")

## Purpose

Establish the event-sourcing foundation of CardCore — a generic, headless C# card game engine — by building the smallest possible end-to-end loop that proves the architecture works. Subsequent slices (real card format, board support, deck-builder migration, simulation runner) extend this skeleton additively without redesigning it.

## Scope

### In scope

- A pure C# library with no Unity dependencies
- Event-sourcing core: `IGameEngine`, `IGameCommand`, `GameEvent` (polymorphic), `GameState`
- Three commands: `StartGameCommand`, `DrawCardCommand`, `PlayCardCommand`
- Three events: `GameStarted`, `CardDrawn`, `CardPlayed`
- Minimal models: `Card`, `Deck`, `Hand`, `Player`
- JSON serialization via `System.Text.Json` with polymorphic event support
- Repository extracted as a Unity Package via Git URL at `com.crosshatch.cardcore`
- Pure-C# xUnit test project at `Tests/PureCSharp/`
- Empty-but-present Unity NUnit assembly at `Tests/Runtime/` for future use
- Strict TDD discipline throughout

### Out of scope (deferred to future slices)

- Snapshot caching for `GetStateAtIndex` (revisit if scrubber feels laggy or simulation runner lands)
- Board / `IGamePiece` / `IBoard` — not even stubs
- Real card format (effects, costs, types) — `Card` is `{ Id, Name }` only
- Turn management, scoring, game-over detection
- Networking
- AI / `IPlayerController` / simulation runner
- Hidden-information / per-player state filtering — handled in UI layer per CLAUDE.md
- UI layer (visualizers, scrubber, prefabs) — separate Unity project, future slice

## Non-Goals

- This slice is **not** a playable game. It demonstrates the engine loop; gameplay rules come with the deck-builder migration.
- This slice does **not** optimize. JSON cloning is the chosen state-copy mechanism; we revisit only if profiling demands it.

## Architecture

CardCore is a pure C# library, no Unity dependencies, packaged as a Unity Package via Git URL at `com.crosshatch.cardcore`. Architecture is event-sourced: commands are the only way to change state, events are the source of truth, and current state is derived by replaying events from index 0.

**Three layers:**

1. **Commands** (`IGameCommand`) — pure functions of `currentState → events[]`. Validate via `CanExecute` and execute via `Execute`. Commands produce events; they do **not** mutate state directly.
2. **Events** (`GameEvent` abstract base + sealed-record subclasses) — immutable records of what happened. Each event carries its own typed payload. Polymorphic JSON via `[JsonDerivedType]` attributes on the base.
3. **State** (`GameState`) — derived from events via `Apply(GameEvent)`. Pattern-matched switch expression dispatches to per-event-type handlers. State is mutable internally but only mutated by `Apply` (which is `internal`); consumers receive cloned snapshots.

**The engine** (`GameEngine : IGameEngine`) is the orchestrator: holds the event log, dispatches commands through `ExecuteCommand`, replays events on demand via `GetStateAtIndex(idx)`, and rebuilds from a saved log via `LoadEventLog`. `GameEngine` is `sealed`. State construction is always: `var s = new GameState(); foreach (e in log) s.Apply(e); return s;` — no constructed-from-thin-air shortcuts. The engine starts with empty state; the first event must be `GameStarted`.

**Determinism is non-negotiable.** Anywhere randomness is needed (deck shuffling), the post-shuffle deck order is captured directly in the `GameStarted` event payload. The seed is also recorded for inspection/regeneration but is **not** used by replay — replay reads the deck order verbatim. This makes every event log fully reproducible across .NET versions, platforms, and engines (Unity, Godot, simulation runners).

**No public mutable state.** Per CLAUDE.md rules: all fields are `private` or `private readonly`, all access is via properties (mostly `init`-only), constructors fully initialize objects, types are `sealed` by default. The only "mutation surface" is `IGameEngine.ExecuteCommand` and `LoadEventLog`.

## Components

### Public surface

- `IGameEngine` — the only entry point. Methods: `ExecuteCommand(IGameCommand)`, `GetEventLog()`, `GetStateAtIndex(int)`, `GetCurrentState()`, `LoadEventLog(IReadOnlyList<GameEvent>)`.
- `GameEngine : IGameEngine, sealed` — the implementation.
- `IGameCommand` — `Execute(GameState) → IReadOnlyList<GameEvent>`, `CanExecute(GameState) → bool`.
- `GameEvent` — abstract base record. Polymorphic root for the event hierarchy. Carries `SequenceId` (int) and `Timestamp` (long, Unix ms). Subclasses add typed fields.
- `GameState` — exposes `Players`, `Deck`, `PlayArea`, `Seed`, `IsStarted` as read-only properties. `Apply(GameEvent)` is `internal` — only the engine calls it.

### Concrete commands

- `StartGameCommand { IReadOnlyList<Card> Deck, int PlayerCount, int Seed }` — `CanExecute` returns true only when state is empty (no `GameStarted` yet). Emits one `GameStarted` event carrying the post-shuffle deck order, player count, and seed.
- `DrawCardCommand { int PlayerId }` — `CanExecute` requires game started, deck non-empty, valid player id. Emits one `CardDrawn` event with `{ PlayerId, CardId, DeckIndexBefore }`.
- `PlayCardCommand { int PlayerId, int HandIndex }` — `CanExecute` requires game started, valid player and hand index. Emits one `CardPlayed` event with `{ PlayerId, CardId, HandIndexBefore, PlayAreaIndexAfter }`.

### Concrete events (sealed records, derive from `GameEvent`)

- `GameStarted { IReadOnlyList<Card> InitialDeckOrder, int PlayerCount, int Seed }` — `InitialDeckOrder` is post-shuffle; `Seed` is recorded for inspection only.
- `CardDrawn { int PlayerId, int CardId, int DeckIndexBefore }`
- `CardPlayed { int PlayerId, int CardId, int HandIndexBefore, int PlayAreaIndexAfter }`

### Models

- `Card` — `sealed record { int Id, string Name }`. Throws `ArgumentException` for `Id < 0` or null/empty `Name`.
- `Deck` — wraps `List<Card>`. Construction takes a seeded `Random` and shuffles. Exposes `Count`, `RemoveTop()` (returns top card and its index), `FindCardById(int)`. No public `Add` — composition is set once at start.
- `Hand` — wraps `List<Card>`. Exposes `Count`, `Add(Card)`, `RemoveAt(int) → Card`, indexer `this[int]`. Owned by `Player`.
- `Player` — `sealed class { int Id, Hand Hand }`. `Hand` is `private readonly`, exposed via property.

### Internal types

- `EventDispatcher` (or static method on `GameState`) — encapsulates the `state.Apply(evt)` pattern-match. Decision deferred to implementation; either is fine.

### JSON polymorphism

`[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]` on `GameEvent` base, `[JsonDerivedType(typeof(GameStarted), "GameStarted")]` etc. on each subtype. Discriminator is the simple type name — stable, human-readable, greppable.

## Data Flow

### Forward path: command → events → state

1. Caller invokes `engine.ExecuteCommand(cmd)`.
2. Engine calls `cmd.CanExecute(state)` — throws `InvalidOperationException` if false.
3. Engine calls `cmd.Execute(state)` — receives `IReadOnlyList<GameEvent>`.
4. Engine assigns `SequenceId` (next in log) and `Timestamp` (now, Unix ms) to each event.
5. Engine appends events to log.
6. Engine calls `state.Apply(evt)` for each event in order.
7. Returns the events to the caller.

**Properties:**

- Commands are pure: `Execute(state)` returns events, never mutates state, never mutates `this`.
- `SequenceId` and `Timestamp` are assigned by the engine, not the command. Commands return events with placeholder values. Keeps commands platform-independent (no clock access in command logic).

### Replay path: events → state

1. Caller invokes `engine.GetStateAtIndex(n)` (or `GetCurrentState()` which is `GetStateAtIndex(log.Count - 1)`).
2. Engine constructs a fresh `new GameState()`.
3. Engine calls `state.Apply(_log[i])` for `i` in `0..n`.
4. Engine clones the state via JSON round-trip (`JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(state))`).
5. Returns the clone.

`LoadEventLog(events)` validates the incoming log (sequence ids contiguous from 0, first event is `GameStarted`, exactly one `GameStarted`), then replays it the same way.

### State copy strategy: mutate-then-clone

The engine holds one live `GameState` instance and mutates it via `Apply`. On read, state is cloned via JSON round-trip. This is faster than pure-immutable replacement (which would allocate a new state per event) and matters at simulation-runner scale. The clone-on-exit prevents consumers from corrupting the live state.

## Error Handling

### Caller-induced errors (recoverable from caller's side)

- `InvalidOperationException` — command failed `CanExecute`. Message names the command type and reason. Caller can call `CanExecute(state)` first for a non-throwing path.
- `ArgumentOutOfRangeException` — `GetStateAtIndex(n)` with `n < 0` or `n >= log.Count`.
- `InvalidOperationException` — `LoadEventLog` was given a malformed log (missing `GameStarted` as event 0, duplicate `GameStarted`, non-contiguous `SequenceId`s, unknown event subtype). Message describes which.

### Invariant violations (engine bugs / tampered logs)

`InvalidOperationException` with the offending event's `SequenceId` in the message. Examples: `Apply(CardDrawn { DeckIndexBefore = 5 })` against a 3-card deck; `Apply(GameStarted)` when game already started. These fail loudly so bugs surface immediately rather than producing silently-wrong state.

### Constructor validation

Per CLAUDE.md "never let an invalid object exist":

- `Card(int id, string name)` — throws `ArgumentException` if `id < 0` or `name` null/empty.
- `StartGameCommand(deck, playerCount, seed)` — throws if `deck` null/empty, `playerCount < 1`, or duplicate `Card.Id` in deck.
- `DrawCardCommand(playerId)` — throws if `playerId < 0`.
- `PlayCardCommand(playerId, handIndex)` — throws if either negative.

### Explicitly NOT doing

- No try/catch inside engine methods. Exceptions propagate.
- No `Result<T>` / monadic error types.
- No silent fallbacks. Quiet half-success is the worst kind of bug in event sourcing.

## Testing

### Frameworks

- **xUnit** for pure-C# tests at `Tests/PureCSharp/CardCore.PureTests.csproj`. Runs via `dotnet test`, no Unity required. This is where ~95% of tests live for this slice (and likely forever).
- **NUnit** under Unity Test Framework at `Tests/Runtime/CardCore.Tests.asmdef` — placeholder, empty for this slice. Future Unity-side integration tests go here.

### Discipline: strict TDD

For every feature in this slice: red → green → refactor. Never write production code without a failing test first.

The user is new to TDD; the implementing session will narrate the rhythm explicitly for the first feature (`Card` validation) and more briefly thereafter.

### Test categories (in order)

1. **Constructor / validation** — one test per invalid-input case for each value type (`Card`, the three commands, `Hand`).
2. **Command tests** — for each command: `CanExecute` true case; `CanExecute` false cases (empty deck, wrong phase, etc.); `Execute` produces the expected events. Pattern-matched event assertions.
3. **Engine integration** — small end-to-end games: `StartGame` → `DrawCard` → `PlayCard`. Assert event log, state at each index, clone-on-exit (mutating the returned state must not affect the engine's live state).
4. **The replay invariant** — the headline test (from CLAUDE.md `EventReplay_ReconstructsIdenticalState`):
   - Run a sequence of commands on engine A.
   - Serialize event log to JSON.
   - Deserialize into engine B via `LoadEventLog`.
   - Assert `JsonSerializer.Serialize(stateA) == JsonSerializer.Serialize(stateB)`.
5. **Error-path tests** — each documented exception (out-of-range index, duplicate `GameStarted`, malformed log, command failing `CanExecute`) gets one test asserting the exception type and a key message fragment.

### Test fixtures

`TestHelpers` class with builders like `TestHelpers.NewStartedGame(deckSize, playerCount)` — but factored only after ~3 tests show the same setup. Premature factoring is a TDD anti-pattern.

### Coverage target

Not a percentage. The target: every command produces the expected events; every event applies cleanly to state; the replay invariant holds. Coverage will be ~95% organically.

## File / Repository Layout

```
cardcore/
├── package.json                          # Unity package manifest
├── README.md
├── claude.md                             # updated to reflect approach 2 (polymorphic events)
├── docs/superpowers/specs/
│   └── 2026-04-27-cardcore-walking-skeleton-design.md
├── Runtime/                              # what Unity ships
│   ├── CardCore.asmdef
│   ├── CardCore.csproj
│   ├── IGameEngine.cs
│   ├── GameEngine.cs
│   ├── GameEvent.cs                      # abstract base
│   ├── GameState.cs
│   ├── Commands/
│   │   ├── IGameCommand.cs
│   │   ├── StartGameCommand.cs
│   │   ├── DrawCardCommand.cs
│   │   └── PlayCardCommand.cs
│   ├── Events/
│   │   ├── GameStarted.cs
│   │   ├── CardDrawn.cs
│   │   └── CardPlayed.cs
│   └── Models/
│       ├── Card.cs
│       ├── Deck.cs
│       ├── Hand.cs
│       └── Player.cs
├── Tests/
│   ├── Runtime/                          # future Unity NUnit tests (empty)
│   │   └── CardCore.Tests.asmdef
│   └── PureCSharp/                       # xUnit, no Unity
│       ├── CardCore.PureTests.csproj
│       └── *.cs
├── CardCore.sln
└── .gitignore
```

## Decisions Log (for traceability)

| # | Question | Decision |
|---|---|---|
| 1 | First slice scope | Walking skeleton (no full surface, no vertical slice) |
| 2 | Serialization | System.Text.Json |
| 3a | Test framework | xUnit (pure-C#) + NUnit (future Unity), split test projects |
| 3b | Test discipline | Strict TDD; teach as we go |
| 4 | Snapshots | None in skeleton |
| 5 | Board stubs | None in skeleton |
| 6 | Command/event surface | 3 commands, 3 events: `StartGame`, `DrawCard`, `PlayCard` → `GameStarted`, `CardDrawn`, `CardPlayed` |
| 7 | `Card` model | `sealed record { int Id, string Name }` |
| 8 | Repository layout | Engine-only repo; Unity Package layout with `Runtime/`, `Tests/PureCSharp/`, future `Tests/Runtime/` |
| Arch | Event payload model | Approach 2: polymorphic `GameEvent` with `[JsonDerivedType]` (NOT CLAUDE.md's `DataJson`-string approach). CLAUDE.md will be updated to reflect this. |
| C2-1 | Deck-position model | Option B: `GameStarted` carries the post-shuffle `InitialDeckOrder`; explicit positions on every deck/hand-touching event |
| C2-2 | `GameState.Apply` visibility | `internal` — only the engine calls it |
| DF | State copy strategy | Mutate-then-clone (live mutated state, JSON-round-trip clone on read) |
| EH | Custom exception types | None — standard exceptions throughout |

## Follow-up tasks (out of this slice)

- Update CLAUDE.md to replace the `GameEvent { DataJson }` shape with the polymorphic hierarchy (done as part of the design-spec commit).
- Future slice: real card format (effects, costs, types).
- Future slice: board / `IBoard` / `IGamePiece`.
- Future slice: turn management, scoring, game-over.
- Future slice: deck-builder migration of the existing UI project.
- Aspirational: simulation runner (`IPlayerController`, `RandomPlayer`, 100k-game runs).
- Aspirational: snapshot caching for `GetStateAtIndex` if/when needed.
