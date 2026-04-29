# Unity Client Doc — Design Spec

**Date:** 2026-04-29
**Status:** Approved
**Slice:** Documentation deliverable for Unity consumers of CardCore (walking-skeleton API)

## Purpose

Produce a single agent-readable Markdown doc that lets a future Claude session (or human dev) take a fresh Unity project from "no CardCore" to "running an event-sourced game loop in a MonoBehaviour" with no further engine knowledge required.

The doc is the symmetric companion to `claude.md`: `claude.md` documents the engine's internal architecture for engine-side work; this doc documents the *consumer-side contract* for client-side work.

## Scope

### In scope

- One markdown file at `Documentation~/unity-client.md`, surfaced via `package.json`'s `documentationUrl` field
- Eight sections (flat H2, with H3 anchors permitted in section 3 only):
  1. Install
  2. 30-second hello world
  3. Public API surface
  4. Calling conventions
  5. Persistence
  6. Adding card data
  7. What this doc does NOT cover
  8. Troubleshooting
- A `package.json` update to set `documentationUrl` so Unity Package Manager links to the doc
- A small addition to `claude.md` stating that this doc must be updated whenever the public API changes
- Pre-ship verification: every code block in the doc compiles against `Runtime/CardCore.csproj`

### Out of scope (deferred)

- Visualizer / scrubber / event-replay UI patterns (covered by `claude.md`; future engine slices and prototype-specific work)
- Animation, prefabs, 3D rendering rules
- Unity-side testing patterns (Unity Test Framework integration)
- Multi-version compatibility matrix (doc lives in the same repo as the engine; version-pinned by Git ref)
- Networking integration

## Non-Goals

- Not a tutorial. The doc assumes the reader is an agent (Claude) or a developer who already understands C# and Unity basics.
- Not a design doc. Design lives in `claude.md` and the walking-skeleton spec; this doc is purely "how to call the public API."
- Not a recipe book. Only the patterns required for the simplest viable client are included.

## Audience and Tone

- **Primary reader:** future Claude sessions retrofitting the existing UI Unity project (and bootstrapping new prototypes).
- **Secondary reader:** future-Dave reviewing or refreshing.
- **Tone:** terse, contract-first. Show C# signatures plus one-line behavior plus one-line use site for every public member. No prose explanations of standard C# / Unity / event-sourcing concepts.

## Architecture (of the doc)

The doc is **flat** — only H2 section headers, with one exception: section 3 (Public API surface) uses H3 anchors per type/method so agents can grep for a specific name.

The doc lives at:

```
cardcore/
└── Documentation~/
    └── unity-client.md
```

Unity Package Manager surfaces a "View documentation" link when the package's `package.json` declares a `documentationUrl`. We will use a relative path:

```json
"documentationUrl": "Documentation~/unity-client.md"
```

The trailing `~` on `Documentation~` is a Unity asset-import convention: it excludes the folder from asset import (no `.meta` files generated, no Library bloat in consumer projects). The doc still ships with the package and is reachable via Package Manager UI.

Length target: 150–200 lines of markdown total. Anything longer means scope has crept beyond "simple Unity client."

## Components (the eight sections)

Each section has one specific job. Success criterion = what an agent should be able to *do* after reading it.

### Section 1 — Install

**Success:** Agent adds the package to a fresh Unity project; import succeeds.

Three lines: open Package Manager → Add package by Git URL → paste `https://github.com/Crosshatch-Games/cardcore.git`. One sentence noting Unity 6.3 LTS minimum.

### Section 2 — 30-second hello world

**Success:** Agent has a working `CardCoreDemo` MonoBehaviour they can attach to any GameObject and see expected output in the Console.

Single ~25-line code block. The script's `Start()` method:

1. Builds a 3-card deck (`new Card(1, "Copper")`, etc.).
2. Instantiates `new GameEngine()`.
3. Executes `StartGameCommand`, then `DrawCardCommand`, then `PlayCardCommand`.
4. Calls `engine.GetCurrentState()` and `Debug.Log`s the result (player hand size, play area count, deck remaining).

Class name: `CardCoreDemo` (per Q6).

### Section 3 — Public API surface

**Success:** Agent can construct any command, read any event, or call any `IGameEngine` method without guessing signatures.

Format per item: C# signature block + one-line behavior + one-line use site. H3 anchors per type/method.

Items covered:

- `IGameEngine` — `ExecuteCommand`, `GetEventLog`, `GetStateAtIndex`, `GetCurrentState`, `LoadEventLog`
- `GameEngine` — constructor only (the rest is via `IGameEngine`)
- `StartGameCommand` — constructor, `CanExecute`, what it emits
- `DrawCardCommand` — constructor, `CanExecute`, what it emits
- `PlayCardCommand` — constructor, `CanExecute`, what it emits
- `GameStarted` — record fields
- `CardDrawn` — record fields
- `CardPlayed` — record fields
- `GameState` — read-only properties: `Players`, `PlayArea`, `Deck`, `Seed`, `IsStarted`
- `Card` — `(int Id, string Name)` plus the validation rules

### Section 4 — Calling conventions

**Success:** Agent avoids the four most common misuses.

Bullet list, no code:

- Commands carry their data via constructor; do not mutate fields after construction.
- `ExecuteCommand` throws `InvalidOperationException` when `CanExecute` returns false; call `CanExecute(state)` first if a non-throwing path is needed.
- `GetCurrentState()` and `GetStateAtIndex(n)` return *cloned* `GameState` objects — modifying them does not affect the engine.
- The event log is the source of truth; persist `engine.GetEventLog()`, never `engine.GetCurrentState()` directly.

### Section 5 — Persistence

**Success:** Agent can save a game to disk and load it back.

~10-line code block: `JsonSerializer.Serialize(engine.GetEventLog())` → write file → read file → `JsonSerializer.Deserialize<List<GameEvent>>(json)` → fresh `new GameEngine()` → `engine.LoadEventLog(events)`.

One sentence noting `LoadEventLog` requires a fresh engine (throws if the engine already has events).

### Section 6 — Adding card data

**Success:** Agent knows how to define a deck for a new prototype.

Three-line snippet: `new Card(id, name)`, build a `List<Card>`, pass to `StartGameCommand`. One-paragraph callout: `Card` is intentionally minimal in this slice; richer card data (effects, costs, types) is a future engine slice. Future additions extend `Card` without breaking this API.

### Section 7 — What this doc does NOT cover

**Success:** Agent does not invent visualizer/scrubber code on top of this doc.

Bulleted negative list:

- Visualizers / `IEventVisualizer` per event type
- Scrubber / `GameEventPlayer` MonoBehaviour
- Async event-replay UI
- Animation, prefab pooling, 3D rendering
- Unity-side testing patterns

Single pointer line: "For visualizer / scrubber / event-replay UI patterns, see `claude.md` at the repo root."

### Section 8 — Troubleshooting

**Success:** Agent recognizes and resolves the three most likely failures in their first 10 minutes.

Three subsections (no H3 — these are inline bullets or short paragraphs):

1. **`InvalidOperationException: Command X failed CanExecute against current state.`** Cause: command can't run against the current state. Fix: call `command.CanExecute(state)` first; if false, inspect state for the missing precondition.
2. **JSON polymorphism: deserialized event has the wrong runtime type.** Cause: deserializing as a concrete subtype (e.g. `Deserialize<CardDrawn>`) bypasses the discriminator. Fix: always declare the base type — `Deserialize<GameEvent>(json)` for one event, `Deserialize<List<GameEvent>>(json)` for a log. Side-by-side wrong/right snippet.
3. **.NET version mismatch.** Cause: package targets `net9.0`. Fix: Unity 6.3 LTS supports `net9.0` natively; older Unity versions will not.

## Data flow (how an agent uses this doc)

A typical agent reading flow:

```
"wire CardCore into a Unity scene"
        ↓
Section 1 (Install) — one-time
        ↓
Section 2 (Hello world) — copy-paste, adapt
        ↓
Section 3 (API surface) — re-read every time a new
                          command/event is needed
        ↓
Section 4 (Calling conventions) — once at first read,
                                 then on exception
        ↓
Section 5 / 6 — on demand for "save game" or "deck"
        ↓
Section 8 — after seeing an error
```

Implications:

- Each H2 must be greppable with one obvious phrase. No clever titles.
- Section 3 is the most-revisited; H3 anchors per public type let agents jump directly.
- No cross-references between sections, except section 7's pointer to `claude.md`. Each section is self-contained.

## Error handling (in the doc itself)

The doc handles three categories of agent-facing failures:

### 1. Install errors (handled implicitly)

Standard Unity Package Manager errors (URL unreachable, network offline, etc.) are general Unity issues, not CardCore-specific. The doc states prerequisites once and trusts the agent to recognize PM errors. No troubleshooting subsection in section 1.

### 2. Runtime exceptions (covered in section 8)

| Exception | Source | Cause | Fix |
|---|---|---|---|
| `InvalidOperationException: Command X failed CanExecute…` | `ExecuteCommand` | Command can't run against current state | Call `CanExecute` first |
| `InvalidOperationException: First event must be GameStarted` etc. | `LoadEventLog` | Malformed event log | Rebuild from scratch |
| `ArgumentOutOfRangeException` | `GetStateAtIndex(n)` | `n < 0` or `n ≥ log.Count` | Validate against `GetEventLog().Count` |
| `ArgumentException` (various) | Constructors | Invalid input | Read message — names the param |

Section 8 documents the top three rows; the rest are self-explanatory from message text.

### 3. JSON polymorphism gotcha (covered in section 8)

The single non-obvious failure: `Deserialize<CardDrawn>` bypasses polymorphism and breaks round-tripping. Fix: always declare the base type. Side-by-side wrong/right example.

### Explicitly NOT in the doc

- No "see GitHub issues" / "file a bug" pointer (solo-dev project).
- No verbose / log-level configuration (CardCore doesn't log; it throws).
- No version compatibility matrix (doc ships with the engine via the same Git ref).

## Verification (before shipping)

Three pre-ship checks:

1. **Every code block compiles.** Each code block in the doc is real C# meant to run in a Unity project. Verification approach: paste each block into a scratch xUnit test in `Tests/PureCSharp/` (translating `Debug.Log` to test asserts and `MonoBehaviour.Start` to a method call), confirm `dotnet test` passes, then **delete the scratch tests** — they're a one-shot doc-correctness check, not permanent.
2. **Every API mention matches source.** Mechanical pass through section 3: each method/command/event/property must exist in `Runtime/` with the documented signature.
3. **Manual Unity install check** (handed to user). Open a fresh Unity 6.3 LTS scratch project, add the package via Git URL, confirm import succeeds and Package Manager UI shows the doc link. Claude cannot run the Unity editor; this checkpoint requires the user to confirm.

## Ongoing maintenance

The doc has one structural risk: **API drift.** If a future engine slice changes a public method/command/event, section 3 silently rots.

Mitigation: add one line to `claude.md` (in the "Your Role" section) instructing future Claude sessions to update `Documentation~/unity-client.md` whenever the public API surface changes. This becomes part of the engine's own maintenance contract.

## File / Repository Layout

```
cardcore/
├── package.json                          # update: add documentationUrl
├── claude.md                             # update: maintenance rule
├── Documentation~/                       # new — the trailing ~ excludes from Unity asset import
│   └── unity-client.md                   # new — this doc
├── docs/superpowers/
│   ├── specs/
│   │   └── 2026-04-29-unity-client-doc-design.md   # this spec
│   └── plans/
│       └── 2026-04-29-unity-client-doc.md          # implementation plan (next step)
├── Runtime/                              # unchanged
└── Tests/                                # unchanged
```

## Decisions log

| # | Question | Decision |
|---|---|---|
| 1 | Audience | A — agent-friendly, dense, contract-first |
| 2 | Scope | A — minimum to call CardCore from a Unity scene; no visualizers |
| 3 | Location | A — `Documentation~/unity-client.md`, surfaced via `documentationUrl` in `package.json` |
| 4 | TOC | All 8 sections approved in proposed order |
| 5 | API density | B — signature + one-line behavior + one-line use site |
| 6a | Doc title | "CardCore for Unity" |
| 6b | Demo class name | `CardCoreDemo` |
| Arch | H3 nesting | Allowed in section 3 only (per-type/method anchors) |
| EH | Custom exception types | None (matches engine spec); document standard exceptions in section 8 |
| Test | Pre-ship verification | Code-block compile in scratch xUnit + manual Unity install (user) |
| Test | Maintenance | `claude.md` rule: update doc when public API changes |

## Follow-up tasks (out of this slice)

- Future doc: visualizer / scrubber / event-replay UI patterns when the existing-UI migration begins or after the third prototype reveals shared patterns.
- Future doc update: when the engine adds a real card format (effects, costs, types), section 6 expands to cover the new shape.
- Future doc update: when board / `IBoard` / `IGamePiece` lands, add a section.
