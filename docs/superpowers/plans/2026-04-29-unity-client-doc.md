# Unity Client Doc — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship `Documentation~/unity-client.md` — an agent-readable doc that takes a Unity consumer from "no CardCore" to "running an event-sourced game loop in a MonoBehaviour" — plus the supporting `package.json` and `claude.md` edits.

**Architecture:** One markdown file at the Unity-Package-Manager-conventional `Documentation~/` path, surfaced to consumers via `package.json`'s `documentationUrl`. Every code block in the doc is verified by a scratch xUnit test that compiles against the real `Runtime/CardCore.csproj`; the scratch test is deleted after verification. Manual Unity install check is handed off to the user (Claude can't run the editor).

**Tech Stack:** Markdown, Unity Package Manager conventions, .NET 9 / C# 12, xUnit (scratch verification only).

**Spec:** `docs/superpowers/specs/2026-04-29-unity-client-doc-design.md`

---

## How to use this plan

- The plan is structured to write the doc section-by-section, verifying each block compiles before moving on. This catches drift between the doc and the real API immediately.
- Tasks 2–9 each produce one section of the doc. Task 10 verifies every snippet compiles. Task 11 finalizes the supporting file edits. Task 12 is the manual hand-off to you.
- **The user has explicit instructions: do not run `git commit` from this plan. Make changes; the user reads the diffs in their editor and commits manually.** Steps that say "ready for review" replace the conventional `git commit` step.
- Per the user's preference, no commits are made by this plan. Skip every `git add` / `git commit` step you'd otherwise expect.

---

## Task 1: Reserve the doc file with the H1 + section skeleton

Create the file with all eight section headers in place, no content yet. This locks the structure in before we fill it in section by section, and gives us a stable anchor map to reference from `package.json`.

**Files:**
- Create: `Documentation~/unity-client.md`

- [ ] **Step 1: Create directory and skeleton file**

Create `Documentation~/unity-client.md` with the following content:

```markdown
# CardCore for Unity

> Headless event-sourced card game engine. This doc covers the minimum to call CardCore from a Unity scene. For visualizer / scrubber / event-replay UI patterns, see `claude.md` at the repo root.

## Install

## 30-second hello world

## Public API surface

## Calling conventions

## Persistence

## Adding card data

## What this doc does NOT cover

## Troubleshooting
```

- [ ] **Step 2: Confirm Unity ignores the folder**

Run from repo root:
```bash
ls -la Documentation~
```
Expected: directory exists, contains `unity-client.md`.

The `~` suffix is the Unity convention that excludes the folder from asset import. No further action needed here; this task only verifies the path exists.

- [ ] **Step 3: Ready for review**

Files changed: new `Documentation~/unity-client.md` (skeleton).

---

## Task 2: Section 1 — Install

**Files:**
- Modify: `Documentation~/unity-client.md` — replace the empty `## Install` section.

- [ ] **Step 1: Replace the `## Install` section**

In `Documentation~/unity-client.md`, replace the line `## Install` (and the empty space below it) with:

```markdown
## Install

**Requirements:** Unity 6.3 LTS or newer.

In Unity: open **Window → Package Manager → ＋ → Add package from git URL…** and paste:

```
https://github.com/Crosshatch-Games/cardcore.git
```

Unity will fetch the package and import it. The `Runtime/` folder becomes the `CardCore` assembly; tests under `Tests/Runtime/` are not imported by default.
```

- [ ] **Step 2: Ready for review**

Files changed: `Documentation~/unity-client.md` (Install section now populated).

---

## Task 3: Section 2 — 30-second hello world

This is the script the agent will copy first. It must compile against `Runtime/CardCore.csproj`. We'll verify that in Task 10; for now, write it carefully and use the same names/types we shipped in the engine.

**Files:**
- Modify: `Documentation~/unity-client.md` — replace the empty `## 30-second hello world` section.

- [ ] **Step 1: Replace the section**

Replace the line `## 30-second hello world` with:

```markdown
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
```

- [ ] **Step 2: Ready for review**

Files changed: `Documentation~/unity-client.md`.

---

## Task 4: Section 3 — Public API surface

The most-revisited section. Uses H3 anchors per type. Format per item: signature + one-line behavior + one-line use site.

**Files:**
- Modify: `Documentation~/unity-client.md` — replace the empty `## Public API surface` section.

- [ ] **Step 1: Replace the section**

Replace the line `## Public API surface` with:

````markdown
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
````

- [ ] **Step 2: Ready for review**

Files changed: `Documentation~/unity-client.md`.

---

## Task 5: Section 4 — Calling conventions

**Files:**
- Modify: `Documentation~/unity-client.md` — replace the empty `## Calling conventions` section.

- [ ] **Step 1: Replace the section**

Replace the line `## Calling conventions` with:

```markdown
## Calling conventions

The four rules a client must follow:

- **Commands carry their data via the constructor.** Build a fresh command per `ExecuteCommand` call. Don't reuse-and-mutate.
- **`ExecuteCommand` throws `InvalidOperationException` when `CanExecute` returns false.** Call `command.CanExecute(state)` first if you want a non-throwing path.
- **`GetCurrentState()` and `GetStateAtIndex(n)` return cloned `GameState` objects.** Modifying the returned state has no effect on the engine — the clone is yours to read or even mutate locally.
- **The event log is the source of truth.** Persist `engine.GetEventLog()`, never the `GameState` directly. State is always derivable from the log.
```

- [ ] **Step 2: Ready for review**

Files changed: `Documentation~/unity-client.md`.

---

## Task 6: Section 5 — Persistence

**Files:**
- Modify: `Documentation~/unity-client.md` — replace the empty `## Persistence` section.

- [ ] **Step 1: Replace the section**

Replace the line `## Persistence` with:

````markdown
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
````

- [ ] **Step 2: Ready for review**

Files changed: `Documentation~/unity-client.md`.

---

## Task 7: Section 6 — Adding card data

**Files:**
- Modify: `Documentation~/unity-client.md` — replace the empty `## Adding card data` section.

- [ ] **Step 1: Replace the section**

Replace the line `## Adding card data` with:

````markdown
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
````

- [ ] **Step 2: Ready for review**

Files changed: `Documentation~/unity-client.md`.

---

## Task 8: Section 7 — What this doc does NOT cover

**Files:**
- Modify: `Documentation~/unity-client.md` — replace the empty `## What this doc does NOT cover` section.

- [ ] **Step 1: Replace the section**

Replace the line `## What this doc does NOT cover` with:

```markdown
## What this doc does NOT cover

Out of scope for "simple Unity client":

- Visualizers (`IEventVisualizer` per event type)
- Scrubber / `GameEventPlayer` MonoBehaviour
- Async event-replay UI
- Animation, prefab pooling, 3D rendering
- Unity-side testing patterns

For visualizer / scrubber / event-replay UI patterns, see `claude.md` at the repo root.
```

- [ ] **Step 2: Ready for review**

Files changed: `Documentation~/unity-client.md`.

---

## Task 9: Section 8 — Troubleshooting

**Files:**
- Modify: `Documentation~/unity-client.md` — replace the empty `## Troubleshooting` section.

- [ ] **Step 1: Replace the section**

Replace the line `## Troubleshooting` with:

````markdown
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
````

- [ ] **Step 2: Ready for review**

Files changed: `Documentation~/unity-client.md`.

---

## Task 10: Verify every code block compiles

This is the integrity check. Every C# code block in the doc gets pasted into a scratch xUnit test that compiles against the real `Runtime/CardCore.csproj`. Tests don't need to *assert* much — the goal is "does this code compile and run without throwing in the happy path?"

**The scratch tests are deleted after verification.** They are not permanent.

**Files:**
- Create (then delete): `Tests/PureCSharp/DocSnippetsTests.cs`

- [ ] **Step 1: Create the scratch verification file**

Create `Tests/PureCSharp/DocSnippetsTests.cs`:

```csharp
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CardCore;
using CardCore.Commands;
using CardCore.Events;
using Xunit;

namespace CardCore.PureTests;

// Scratch: every code block from Documentation~/unity-client.md.
// Goal: confirm the snippets compile and run in the happy path.
// This file is deleted after verification — it's not a permanent test.
public class DocSnippetsTests
{
    // Section 2 — 30-second hello world. Translated: Start() body, Debug.Log → no-op.
    [Fact]
    public void Section2_HelloWorld_Compiles_And_Runs()
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
        Assert.Equal(0, state.Players[0].Hand.Count);
        Assert.Equal(1, state.PlayArea.Count);
        Assert.Equal(2, state.Deck!.Count);
    }

    // Section 3 — every "Use:" line.
    [Fact]
    public void Section3_UseLines_Compile_And_Run()
    {
        var engine = new GameEngine();

        // ExecuteCommand use site
        engine.ExecuteCommand(new StartGameCommand(
            new List<Card> { new(1, "A"), new(2, "B") }, playerCount: 1, seed: 0));

        // GetEventLog use site
        var log = engine.GetEventLog();
        Assert.NotEmpty(log);

        // GetStateAtIndex use site
        var snapshot = engine.GetStateAtIndex(0);
        Assert.True(snapshot.IsStarted);

        // GetCurrentState use site
        var state = engine.GetCurrentState();
        Assert.True(state.IsStarted);

        // DrawCard / PlayCard use sites
        engine.ExecuteCommand(new DrawCardCommand(playerId: 0));
        engine.ExecuteCommand(new PlayCardCommand(playerId: 0, handIndex: 0));

        // LoadEventLog use site
        var json = JsonSerializer.Serialize(engine.GetEventLog());
        var deserializedEvents = JsonSerializer.Deserialize<List<GameEvent>>(json)!;
        var fresh = new GameEngine();
        fresh.LoadEventLog(deserializedEvents);
        Assert.Equal(engine.GetEventLog().Count, fresh.GetEventLog().Count);
    }

    // Section 5 — Persistence block (with File I/O substituted for an in-memory string).
    [Fact]
    public void Section5_Persistence_Compiles_And_Runs()
    {
        var sourceEngine = new GameEngine();
        sourceEngine.ExecuteCommand(new StartGameCommand(
            new List<Card> { new(1, "A") }, playerCount: 1, seed: 0));

        // Save (string instead of File for testability; same JSON)
        var json = JsonSerializer.Serialize(sourceEngine.GetEventLog());

        // Load
        var loadedJson = json;
        var events = JsonSerializer.Deserialize<List<GameEvent>>(loadedJson)!;
        var engine = new GameEngine();
        engine.LoadEventLog(events);

        Assert.Equal(1, engine.GetEventLog().Count);
    }

    // Section 6 — Adding card data block.
    [Fact]
    public void Section6_AddingCardData_Compiles_And_Runs()
    {
        var engine = new GameEngine();
        var deck = new List<Card>
        {
            new Card(1, "Copper"),
            new Card(2, "Silver"),
            new Card(3, "Gold"),
        };
        engine.ExecuteCommand(new StartGameCommand(deck, playerCount: 2, seed: 42));
        Assert.True(engine.GetCurrentState().IsStarted);
    }

    // Section 8 — Troubleshooting "Wrong / Right" example.
    [Fact]
    public void Section8_Troubleshooting_PolymorphismExample_Compiles()
    {
        var engine = new GameEngine();
        engine.ExecuteCommand(new StartGameCommand(
            new List<Card> { new(1, "A") }, playerCount: 1, seed: 0));
        var oneEventJson = JsonSerializer.Serialize<GameEvent>(engine.GetEventLog()[0]);
        var listJson = JsonSerializer.Serialize(engine.GetEventLog());

        // The "right" branch — both should compile and produce typed objects.
        var oneEvent = JsonSerializer.Deserialize<GameEvent>(oneEventJson);
        var log = JsonSerializer.Deserialize<List<GameEvent>>(listJson);

        Assert.IsType<GameStarted>(oneEvent);
        Assert.NotNull(log);
        Assert.Single(log!);
    }
}
```

- [ ] **Step 2: Run the scratch tests**

Run from repo root:
```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter "FullyQualifiedName~DocSnippetsTests"
```

Expected: `Passed: 5, Failed: 0`. If any test fails, the corresponding doc snippet is wrong — fix the doc, then re-run.

- [ ] **Step 3: Run the full suite to confirm no regressions**

Run from repo root:
```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```

Expected: `Passed: 70, Failed: 0` (65 existing + 5 scratch).

- [ ] **Step 4: Delete the scratch file**

```bash
rm Tests/PureCSharp/DocSnippetsTests.cs
```

- [ ] **Step 5: Re-run the full suite**

Run from repo root:
```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```

Expected: `Passed: 65, Failed: 0`. Suite is back to its original size.

- [ ] **Step 6: Ready for review**

Files changed (net): none — scratch file was created and deleted in this task. The verification result is the doc itself, which is now known to compile.

---

## Task 11: Update `package.json` and `claude.md`

Two small file edits to make Unity Package Manager find the doc and to lock in the maintenance rule.

**Files:**
- Modify: `package.json`
- Modify: `claude.md`

- [ ] **Step 1: Add `documentationUrl` to `package.json`**

Open `package.json`. Add a new line for `documentationUrl` between `description` and `unity`. Final file should read:

```json
{
  "name": "com.crosshatch.cardcore",
  "version": "0.0.1",
  "displayName": "CardCore",
  "description": "Headless event-sourced card game engine.",
  "documentationUrl": "Documentation~/unity-client.md",
  "unity": "6000.3",
  "author": {
    "name": "Crosshatch Games"
  }
}
```

- [ ] **Step 2: Verify the JSON is valid**

Run from repo root:
```bash
python3 -m json.tool package.json > /dev/null && echo "OK"
```
Expected: `OK`. (If you don't have Python, any JSON validator works.)

- [ ] **Step 3: Add the maintenance rule to `claude.md`**

Open `claude.md`. Find the section `# \#\#Your Role (Claude CLI)`. After the existing line about diagrammatic representations:

```
You also need to maintain up to date diagrammatic representations of the code base. This should be updated every time you update the project code.
```

Add a new paragraph:

```
You also maintain `Documentation~/unity-client.md`. Whenever the public API surface (`IGameEngine`, `IGameCommand`, `GameEvent` subtypes, `Card`, `GameState` properties) changes — added, removed, renamed, or signature-changed — update this doc in the same change. The doc is what every Unity consumer reads; if it drifts, every prototype that imports CardCore is wrong.
```

- [ ] **Step 4: Ready for review**

Files changed: `package.json`, `claude.md`.

---

## Task 12: Manual Unity install check (handed to user)

Claude cannot run the Unity editor. This is a checkpoint where the user verifies the doc actually works as advertised. Do not skip this.

**Files:** none (this is a procedural step).

- [ ] **Step 1: Hand off to the user**

Tell the user:

> Doc is written and every code block has been compiled against the engine. One manual check remains:
>
> 1. Open Unity Hub, create a fresh empty 3D project on Unity 6.3 LTS.
> 2. In the new project, open **Window → Package Manager → ＋ → Add package from git URL…** and paste:
>    ```
>    https://github.com/Crosshatch-Games/cardcore.git
>    ```
> 3. Wait for import to finish.
> 4. In Package Manager, click on the CardCore package. Confirm the "View documentation" link is present and clicking it opens our `Documentation~/unity-client.md` (either in browser or in a markdown viewer).
> 5. Optional sanity check: copy the `CardCoreDemo` script from the doc into `Assets/Scripts/CardCoreDemo.cs`, attach it to an empty GameObject in a scene, press Play, confirm the Console shows `Hand: 0  PlayArea: 1  Deck: 2`.
>
> Tell me when you've confirmed steps 1–4 (and 5 if you ran it).

- [ ] **Step 2: Wait for user confirmation**

Block on the user's response. If anything fails (e.g. "documentation link doesn't appear"), revisit Task 11's `documentationUrl` value — Unity may have changed conventions or expect a slightly different path format.

- [ ] **Step 3: Mark plan complete**

Once the user confirms, the plan is complete.

---

## Done

At the end of Task 12, the deliverables are:

- `Documentation~/unity-client.md` — 8 sections, every code block known to compile against the real engine.
- `package.json` — `documentationUrl` set so Unity Package Manager surfaces the doc.
- `claude.md` — maintenance rule binding future Claude sessions to keep the doc in sync.
- The full xUnit suite still passes at 65/65 (scratch tests removed).
- User has verified end-to-end install on a real Unity project.

Future doc work (out of scope for this plan):

- Visualizer / scrubber / event-replay UI doc (when the existing-UI migration begins or after the third prototype reveals shared patterns).
- Section 6 expansion when `Card` gains a richer format (effects/costs/types).
- New section when board / `IBoard` / `IGamePiece` lands.
