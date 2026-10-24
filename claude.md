# CardCore

## Mission

Dave is a solo game designer with decades of experience. CardCore is a tool for rapidly prototyping digital-first board and card games. If a game succeeds, printing comes later.

- Generic enough to build several games on it.
- Modular, strongly encapsulated, maximum reuse.
- Headless. Used from several Unity projects, possibly Godot later.
- Dave must understand how it works. He may request familiar design patterns. If a pattern doesn't fit or you have a better one, say so.

## Your role

You create, test and maintain the CardCore service and enforce the standards games are built on. You are a service provider to client projects.

- Keep diagrams of the code base current. Update them with every code change.
- Update `docs~/Documentation~/unity-client.md` **in the same change** as any public API change (engine, commands, events, `GameState`, cards, catalog, markdown, ruleset, actions). Every consumer reads it. Drift breaks them.
- Distributed as a Unity Package via Git URL (`com.crosshatch.cardcore`). Clients import the latest.

## Architecture

Event sourcing. Commands produce events. Events are the source of truth. State is derived by replaying events.

- `IGameEngine` / `GameEngine` (sealed): `ExecuteCommand`, `GetEventLog`, `GetStateAtIndex`, `GetCurrentState`, `LoadEventLog`, `GetDeckCount`, `GetDiscardCount`, `MutateLiveCardAction`.
- `IGameCommand`: `CanExecute(state)`, `Execute(state)`. Commands never mutate state.
- `GameState.Apply` is `internal`. Only the engine calls it. Consumers get cloned state (JSON round-trip).
- Determinism: RNG results are captured in the event payload. Replay never re-runs RNG.
- Events: `GameStarted`, `CardDrawn`, `CardPlayed`, `CardDiscarded`, `CardDestroyed`, `DiscardMovedToDeck`, `DeckShuffled`, `RulesetEvent`.
- Commands: `StartGame`, `DrawCard`, `PlayCard`, `Discard`, `DestroyCard`, `MoveDiscardToDeck`, `ShuffleDeck`, `EmitRulesetEvent`.
- `RulesetEvent(kind, payload)` is opaque to the engine. Rulesets derive their own state from it. `Apply` ignores it.
- UI is not in this package. Unity UI lives in client projects, consumes CardCore through `IGameEngine` only, never mutates state directly.

## Rules (non-negotiable)

Encapsulation
- All fields `private` / `private readonly`. No `public` or `internal` fields. Expose state via properties.
- Properties `init`-only or `private set`. Full `set` only when external mutation is genuinely required.
- `readonly record struct` / `record` for value data. `sealed class` for stateful types. Default to `sealed`.
- Validate in constructors. An invalid object must never exist. No two-phase init.
- Mutating operations are methods on the owning class.
- Prefer `internal` unless the type is public API. Use `[InternalsVisibleTo]` for tests.

Unity compatibility
- No `using UnityEngine`, no `MonoBehaviour`. `System.Random`, never `UnityEngine.Random`.
- Newtonsoft.Json only. Never `System.Text.Json`. Polymorphic events go through `GameEvent.JsonSettings`.
- Explicit `using`s in every file. No reliance on implicit usings.
- Every `.cs` file has a `.meta` file.
- Do not remove `Runtime/Internal/IsExternalInit.cs`.
- Everything public is JSON-serializable.
- `dotnet test` passing is not enough. Real validation is a clean import into Unity 6.3 LTS. Say so when asmdef, csproj or `package.json` change.

## Layout

```
Runtime/        engine: Commands/ Events/ Models/ Catalog/ Markdown/ Ruleset/ Internal/
Tests~/         PureCSharp/ (xUnit, primary), Runtime/ (Unity NUnit stub)
docs~/         Documentation~/ (unity-client.md, authoring docs), architecture.md, superpowers/ (specs, plans). Trailing ~ = Unity ignores
```

## Testing

- Run: `cd Tests~/PureCSharp && dotnet test`.
- Demo harness: `Tests~/Demo/CardCore.Demo.csproj` for human-readable end-to-end output.
- Replay tests: a log loaded into a fresh engine must produce identical state.

## Conventions

- Classes, methods, properties `PascalCase`. Private fields `_camelCase`. Interfaces `IPascalCase`.

## Scope decisions (from Dave)

- First game: a deck-building game of Dave's design. An existing hand-built Unity UI will be converted to use CardCore.
- Order of work: core, then testing, then game UI, then simulation.
- Simulation (AI players, bulk runs) is aspirational. Keep it in mind for architecture.
- Rules are to become headless later (`IRuleset`).
- Hidden information is handled in the UI layer. Cheating is not a concern yet.
- Board functionality comes sooner than later. Keep stubs only until then.
- Networking: stay compatible, do not implement.
- No snapshot optimization yet.
- Cards use Dave's own rich card format (`CardDefinition`, `CardInstance`, `CardCatalog`).
