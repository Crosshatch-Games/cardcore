---
name: cardcore-conventions
description: Use when modifying any code under Runtime/ or Tests/ in the CardCore repo — adding events, commands, models, refactoring engine internals, writing tests, updating package config, or extending the public API surface
---

# CardCore Conventions

## Overview

CardCore is a pure C# event-sourced card-game engine, distributed as a Unity Package via Git URL. The conventions here are **non-negotiable** — they exist because the codebase is consumed by Unity (which has its own quirks) and by future ports (Godot, simulation runners) which need a strict, predictable API surface.

If you are about to write CardCore code and have NOT read this skill, stop and read it. Violating these rules will break Unity imports silently and force a future cleanup session.

## When to Use

- Adding a new model, event, command, or interface to `Runtime/`
- Modifying any file under `Runtime/` or `Tests/PureCSharp/`
- Updating `Runtime/CardCore.asmdef`, `package.json`, or asmdef-related config
- Writing tests under `Tests/PureCSharp/`
- Updating `Documentation~/unity-client.md` (required when public API changes)

## Hard rules (encapsulation)

These come from `CLAUDE.md` at the repo root. They are not stylistic preferences.

- **All fields are `private` or `private readonly`.** No `public` fields. No `internal` fields. State is exposed via properties only.
- **Properties are `init`-only or `private set` by default.** Use full `set` only when external mutation is genuinely required (it almost never is).
- **Use `readonly record struct` for value-like data.** Use `record` (or `sealed record`) for immutable reference types. Use `sealed class` for stateful types.
- **Validate in constructors.** Never let an invalid object exist. An invalid object is a bug; an exception at construction is the correct response.
- **Mutating operations live as methods on the owning class.** External code does not reach into properties to mutate. The mutation method enforces invariants.
- **Prefer `internal` over `public`** unless the type is part of the public API surface (`IGameEngine`, `IGameCommand`, `GameEvent` and subtypes, `GameState`, `Card*`, `Deck`, `Hand`, `Player`, the catalog/markdown/ruleset types, the Action types).
- **Use `[InternalsVisibleTo]`** for test access and for the (future) ruleset assembly.
- **Constructors fully initialize the object.** No two-phase init. No `Initialize()` method called after `new`.
- **Default to `sealed`** for classes. Inheritance is opt-in, not opt-out.

## Hard rules (Unity compatibility)

These come from `feedback_unity_package_gotchas`. Every one of these has burned us before.

- **No `using UnityEngine`.** No `MonoBehaviour`. No `UnityEngine.Random`. Use `System.Random` only. Runtime is pure C#.
- **Explicit `using` statements at the top of every `.cs` file.** Implicit usings (the `<ImplicitUsings>enable</ImplicitUsings>` csproj feature) are a dotnet-only feature; Unity ignores them. A file that compiles via `dotnet build` may fail in Unity if it relies on implicit usings.
- **Newtonsoft.Json, never `System.Text.Json`.** Unity packaging requires Newtonsoft. Polymorphic serialization on `GameEvent` uses Newtonsoft's `TypeNameHandling`, configured via `GameEvent.JsonSettings`.
- **`init`-only properties require `IsExternalInit` polyfill.** Already provided at `Runtime/Internal/IsExternalInit.cs`. Don't delete it. Don't move it. New `init`-only types just work because of it.
- **Every `.cs` file needs a sibling `.meta` file.** Unity ignores files without `.meta`. The repo already has them; if you `mv` or `rm`, update the `.meta` accordingly. New files need new `.meta` files (Unity generates these on import, but pre-generating one keeps git diffs clean).
- **`dotnet test` passing is necessary but not sufficient.** Real validation is "the package imports cleanly into Unity 6.3 LTS." If you've changed the asmdef, csproj, or anything in `package.json`, mention this gap in your handoff so the user can verify.
- **`Documentation~/` is a Unity-special folder.** The trailing `~` tells Unity to ignore it. Documentation, sample data, and authoring artifacts go here.

## Architecture invariants

- **Event sourcing is the law.** Commands → events → state. Commands never mutate state; they produce events. State derives from events via `GameState.Apply(GameEvent)`. The event log is the source of truth.
- **`GameState.Apply` is `internal`.** Only `GameEngine` calls it. Consumers receive **cloned** state via `GetCurrentState()` / `GetStateAtIndex()`.
- **Cloning is JSON round-trip.** `GameState.Clone()` serializes and deserializes via Newtonsoft. Anything held by `GameState` must round-trip correctly.
- **Determinism is non-negotiable.** Anywhere RNG is needed (deck shuffle), the result is captured into the relevant event payload. Replay reads payloads verbatim — never re-runs RNG. The seed is recorded for inspection only.
- **`GameEngine` is sealed.** `IGameEngine` is the contract. Subclassing `GameEngine` is not part of the design.

## File layout

```
Runtime/
├── CardCore.asmdef             # asmdef — defines the runtime assembly
├── CardCore.csproj             # for dotnet tooling
├── GameEngine.cs               # IGameEngine implementation
├── GameEvent.cs                # abstract base + JsonSettings
├── GameState.cs                # the derived state, Apply, Clone
├── IGameEngine.cs              # public contract
├── Commands/                   # IGameCommand implementations
├── Events/                     # GameEvent subtypes (sealed records)
├── Models/                     # Card-related, Deck, Hand, Player
├── Markdown/                   # (future) MarkdownText, MarkdownToken, MarkdownParser
├── Catalog/                    # (future) CardCatalog, CardCatalogLoader
├── Ruleset/                    # (future) IRuleset, IActionHandler, ActionDispatcher
└── Internal/                   # IsExternalInit polyfill, anything not part of the public API

Tests/
├── PureCSharp/                 # xUnit, pure C# — primary test surface
└── Runtime/                    # Unity NUnit assembly — empty stub for future use

Documentation~/
├── unity-client.md             # public API surface, what every consumer reads
├── Claude MD - Cardcore Cards.md  # author's card concept doc
└── *.csv                       # designer authoring artifacts (NOT loaded at runtime)

docs/superpowers/
├── specs/                      # design docs (per slice)
└── plans/                      # implementation plans (per slice)
```

## Doc maintenance (Documentation~/unity-client.md)

**Updating `unity-client.md` is part of any change to the public API surface.** Not a follow-up. Same change.

The public API surface includes:

- `IGameEngine` and `GameEngine` (any signature change)
- `IGameCommand` and concrete commands (constructors, behavior)
- `GameEvent` and its sealed-record subtypes (added/removed/renamed events, payload changes)
- `GameState` properties
- `Card*`, `Deck`, `Hand`, `Player` properties
- (Future) `CardDefinition`, `CardInstance`, `CardCatalog`, `CardCatalogLoader`, `MarkdownText`, `MarkdownToken`, `Action`, `IRuleset`, `IActionHandler`, `ActionDispatcher`

If you added or changed any of those without updating `unity-client.md`, the work is not complete.

## Common mistakes

| Mistake | Fix |
|---|---|
| Used `using System.Text.Json` | Replace with Newtonsoft.Json. Polymorphism via `[JsonConverter]` + `JsonSerializerSettings`, not `[JsonDerivedType]`. |
| Added a `public` field for "convenience" | Convert to property. Use `init` if you can, `private set` if you must. |
| Created a `MonoBehaviour` somewhere | Wrong project. CardCore is headless. Move it to a Unity client project. |
| Used `UnityEngine.Random` | Replace with `System.Random` (passed in via constructor for testability). |
| Added a new `.cs` file without `.meta` | Generate `.meta` (any UUID; Unity will accept it). Or move on and remember Unity will create one on import. |
| Wrote `[JsonDerivedType]` on a record | That's `System.Text.Json`. Use Newtonsoft's `TypeNameHandling.Auto` via `GameEvent.JsonSettings`. |
| Wrote a `GameEngine` subclass | Don't. `GameEngine` is sealed by design. Compose, don't inherit. |
| Skipped updating `unity-client.md` | Update it now. The doc is what every consumer reads. Drift = broken consumers. |
| Two-phase init: `var x = new X(); x.Initialize(...)` | One-phase: `var x = new X(...)`. Constructor takes everything needed. |
| Put gameplay logic in `GameEngine` | Wrong layer. `GameEngine` is an event log + dispatch. Gameplay logic belongs in commands and (future) the ruleset. |

## Red flags — STOP

- About to write `using UnityEngine` in `Runtime/`
- About to make a field `public` or `internal`
- About to write a non-`sealed` class without a clear inheritance design
- About to call `Apply()` from outside `GameEngine`
- About to use `System.Text.Json` anywhere
- About to add a public type without updating `unity-client.md`
- About to skip a constructor validation because "the caller will check"
- About to add a setter "just in case"

If any of these is true: stop, re-read this skill, and pick the conforming path.

## Don't commit unless asked

The user reads diffs in their editor. Do **not** run `git commit` unless they explicitly ask. Leave the working tree dirty for review. (See `feedback_no_commits` in the user's auto-memory.)
