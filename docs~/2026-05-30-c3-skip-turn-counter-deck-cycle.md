# Slice C.3 — Skip Turn, Turn Counter, Real Deck Cycle Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver the first real "turn loop." An End Turn button (bottom-left HUD) calls `GameRules.EndTurn()`; each card left in hand resolves to its authored `skipFate` (Discard / Destroy / Upgrade-as-Discard); the player draws a new hand of `_openingHandSize`; the turn counter advances and a top-left `TurnCounterHUD` repaints. The deck shrinks to `_deckCopiesPerCardDefinition = 2` (18 cards today), forcing real reshuffles. Reshuffle policy lives in GameRules — when draw finds an empty deck, GameRules issues `MoveDiscardToDeckCommand` + `ShuffleDeckCommand` and retries; if discard is also empty, `OnDrawFailed` fires (C.4 lose-condition hook).

**Architecture:** GameRules grows a `CurrentTurn`, an `EndTurn()` entry point, a `TryDraw(p)` helper that owns the reshuffle loop, and four new C# events (`OnTurnEnded`, `OnReshuffleStarting`, `OnReshuffleCompleted`, `OnDrawFailed`). `DiscardFate` is a new enum in `Code.Scripts.Rules`. `HandCard` DTO grows a `SkipFate` field. Card JSON gains an optional `"skipFate"` field (default `"discard"`). Two new scene-resident MonoBehaviours (`EndTurnButton`, `TurnCounterHUD`) are Pattern A — `[SerializeField]`-wired. The CardCore engine PR (separate repo, separate plan) adds `DiscardCommand`, `DestroyCardCommand`, `MoveDiscardToDeckCommand`, `ShuffleDeckCommand`, peek accessors, and a `DiscardPile` collection on per-player state. The engine PR ships first; this plan bumps the CardCore pin and then proceeds.

**Tech Stack:** Unity 6.3 LTS, C#, CardCore (`com.crosshatch.cardcore` Unity Package — pin will bump from `05682c93616b` to the SHA produced by the engine PR), Newtonsoft.Json, Unity Test Framework / NUnit, TextMesh Pro.

**Reference spec:** `docs/superpowers/specs/2026-05-30-c3-skip-turn-counter-deck-cycle-design.md`

---

## File Structure

### Created (production)

- `Assets/Code/Scripts/GameRules/DiscardFate.cs` — enum `{ Discard, Destroy, Upgrade }`. Plain C#, lives in `Code.Scripts.Rules`. C.4 zone code will `using` this directly.
- `Assets/Code/Scripts/GameRules/DiscardFateParser.cs` — static `Parse(string raw)` (case-insensitive; warn-and-default to `Discard` for unknown or empty). Parallel to `CurrencyIdParser`.
- `Assets/Code/Scripts/GameRules/DrawOutcome.cs` — enum `{ Drew, NoCardsLeft }`. The return type of `GameRules.TryDraw`.
- `Assets/Code/Scripts/UI/EndTurnButton.cs` — Pattern A scene-resident MonoBehaviour. Wraps a `ColliderButton`; on click calls `_gameRules.EndTurn()`. Bottom-left of HUD.
- `Assets/Code/Scripts/UI/TurnCounterHUD.cs` — Pattern A scene-resident MonoBehaviour. Subscribes to `GameRules.OnTurnEnded`, paints `_label.text = $"Turn {turn}"`. Top-left of HUD.

### Created (tests)

- `Assets/Tests/EditMode/DiscardFateParserTests.cs` — case-insensitive parse, default-on-unknown, default-on-null/empty.
- `Assets/Tests/EditMode/SkipFateJsonContractTests.cs` — JSON contract: top-level `"skipFate"` round-trips through `CardCatalogLoader` and is readable from the `CardDefinition` (via `CardInstance.GetSkipFateRaw()` or the documented exposure path — see Pre-flight context).
- `Assets/Tests/EditMode/HandCardSkipFateTests.cs` — `HandCard.SkipFate` is populated from the card definition's raw field, defaults to `Discard` when absent, defaults to `Discard` + warns on unknown values.
- `Assets/Tests/EditMode/EndTurnTests.cs` — `EndTurn` increments `CurrentTurn`, fires `OnTurnEnded`, dispatches discard/destroy/upgrade fates correctly, draws a fresh hand of `_openingHandSize`, still advances on empty hand, fires `OnStateChanged` exactly once per call.
- `Assets/Tests/EditMode/TryDrawReshuffleTests.cs` — empty-deck-with-discard triggers reshuffle in order (`OnReshuffleStarting` → engine commands → `OnReshuffleCompleted` → `DrawCommand`); empty-deck-and-discard fires `OnDrawFailed` and returns `NoCardsLeft`; defensive guard (post-reshuffle deck still empty) fires `OnDrawFailed`; reshuffle with empty discard is not requested.
- `Assets/Tests/EditMode/DeckConstructionTests.cs` — `_deckCopiesPerCardDefinition` controls the deck count (parameterized).

### Modified

- `Assets/Code/Scripts/GameRules/GameRules.cs` — new public surface (`CurrentTurn`, `DeckCount`, `DiscardCount`, `EndTurn()`, four events), private `TryDraw(int)` helper, swap `_copiesPerCard = 40` to `_deckCopiesPerCardDefinition = 2`, swap `_initialHandSize` Inspector label to `_openingHandSize` (keeps the field used for both opening and redraw). `StartLevel()` initializes `_currentTurn = 1`.
- `Assets/Code/Scripts/GameRules/HandCard.cs` — gains a `DiscardFate SkipFate` property and ctor arg.
- `Assets/StreamingAssets/Cards/wood_resource.json` — gains `"skipFate": "discard"` (no behavior change; documents intent).
- `Assets/StreamingAssets/Cards/marsh.json` — gains `"skipFate": "destroy"` (Piece card chosen because destroying a piece-spawning card is the most visually distinct test case; "marsh" reads as a fragile/temporary card).
- `Assets/StreamingAssets/Cards/water_resource.json` — gains `"skipFate": "upgrade"` (treated as Discard this slice; documents future upgrade semantics).
- `Assets/Scenes/BlankLevel.unity` — adds an `EndTurnButton` GameObject (bottom-left HUD) and a `TurnCounterHUD` GameObject (top-left HUD) and wires both to `GameRules` via Inspector. Scene change is **manual** — the agent cannot edit `.unity` files reliably. Documented in Task 13.

### Engine prerequisite (separate repo, not this plan)

The CardCore engine PR must land and produce a new package SHA before Task 1 begins. The engine PR is described in detail in the spec under "Engine prerequisite — CardCore PR." This plan's Task 0 verifies the new SHA is pinned in `Packages/manifest.json`.

---

## Pre-flight context

- **Reading order before any task.** Read `docs/superpowers/specs/2026-05-30-c3-skip-turn-counter-deck-cycle-design.md` end-to-end, then `Packages/com.crosshatch.cardcore/Documentation~/unity-client.md` (post-pin-bump version). The spec is the contract; `unity-client.md` is the authoritative engine API.
- **CardCore pin.** Pre-bump: `05682c93616b` (no C.3 commands present). Post-bump SHA is whatever the engine PR produces; Task 0 records it. Verify post-bump by reading `Library/PackageCache/com.crosshatch.cardcore@<sha>/Runtime/Commands/` and confirming `DiscardCommand.cs`, `DestroyCardCommand.cs`, `MoveDiscardToDeckCommand.cs`, `ShuffleDeckCommand.cs` exist.
- **Branch.** Work on a feature branch `feature/c3-skip-turn-deck-cycle`. The spec is already on `main` at commit `d2e95dd`. The first commit on the branch is the pin bump (Task 0).
- **No autonomous commits.** Project convention: every task ends with `git add` + `git status`; user runs `git commit` themselves.
- **`HandCard` ctor call site.** Single call site at `Assets/Code/Scripts/GameRules/GameRules.cs:141` (`new HandCard(...)`). Updating the ctor signature touches exactly one production line outside the DTO file.
- **`StartLevel()` initial hand size.** Line 79 reads `_initialHandSize`; the same field will be reused for the post-skip redraw loop. The spec calls it `_openingHandSize`. The plan renames the field to `_openingHandSize`. **This is a SerializeField rename** — Unity stores serialized fields by name in the scene; renaming the field will silently lose the Inspector value in `BlankLevel.unity`. Mitigation: add `[FormerlySerializedAs("_initialHandSize")]` from `UnityEngine.Serialization` so the existing value migrates. Documented in Task 2.
- **`_copiesPerCard = 40` → `_deckCopiesPerCardDefinition = 2`.** Same `[FormerlySerializedAs]` story. The default value in code changes; the Inspector value in `BlankLevel.unity` will migrate via the attribute. After Stage B the user will need to re-save the scene to lock in the new default.
- **Boundary 2 holds.** Only `GameRules.cs`, the new handler logic, and (for the DTO field) `HandCard.cs` reference CardCore types. `EndTurnButton`, `TurnCounterHUD`, and the parser do not `using CardCore;`.
- **`OnStateChanged` posture.** `EndTurn()` fires `OnStateChanged` **once**, at the end, after all discards and draws. Per-discard `OnStateChanged` would cause N hand repaints; we want one. The tests pin this.
- **`localPlayerIndex`.** Single-player project; `0` everywhere. The pseudo-code in the spec uses `p`; production code uses the literal `0` (matching `StartLevel`, `ConfirmPlay`, `GetHand`).
- **`OnDrawFailed` semantics.** Fires once per failed `TryDraw` call. If the redraw loop encounters empty-and-empty, `OnDrawFailed` fires once per attempted draw (so a 5-card redraw on an empty deck-and-discard fires 5 times). That's per-attempt, not per-turn. Tests pin this. C.4 lose-condition will likely debounce on its side or react to the first one.
- **`Upgrade` fate dispatch.** This slice: `case Upgrade: engine.ExecuteCommand(new DiscardCommand(0, id)); /* TODO(C.4): real upgrade semantics */`. The fate value is preserved on `HandCard.SkipFate` so future replay sees the player's intent.
- **JSON `skipFate` field exposure via CardCore.** CardCore's `CardDefinition` doesn't natively know about `skipFate`. The engine PR adds nothing for it — `skipFate` is **client metadata**, not engine metadata. So how does the client read it from a `CardInstance`?

  Two options exist; the engine PR must clarify which (and the spec defers this to the engine PR's API surface):

  1. **CardCore adds a generic `IReadOnlyDictionary<string, string> Metadata { get; }` on `CardDefinition`** that round-trips any unknown top-level JSON fields. Client reads `def.Metadata["skipFate"]`. This is the cleanest if CardCore agrees.
  2. **The client re-parses the JSON.** GameRules holds the raw JSON dict by card id (loaded once at `Awake`) and reads `_rawJson[cardId]["skipFate"]` when populating `HandCard`. Engine stays untouched.

  **This plan assumes Option 2** (client re-parses) so it can proceed without further engine coordination. Task 4 creates a `CardJsonMetadata` loader. If the engine PR ends up adopting Option 1, Task 4's loader becomes a thin proxy and the test contract still holds.

- **`Library/PackageCache/com.crosshatch.cardcore@<sha>/`** is read-only — Unity regenerates it from the git pin. Don't edit it.
- **CardCore types referenced.** `IGameEngine`, `GameEngine`, `CardInstance`, `CardCatalog`, `CardCatalogLoader`, `CardDefinition`, plus the new C.3 engine PR types (`DiscardCommand`, `DestroyCardCommand`, `MoveDiscardToDeckCommand`, `ShuffleDeckCommand`). All public on the post-bump package.

---

## Task 0: Verify engine prereq is pinned

**Files:**
- Modify: `Packages/manifest.json` (commit hash bump)

- [ ] **Step 1: Confirm the engine PR has merged**

The CardCore engine PR (described in the spec) must be merged and tagged. Ask the user for the new SHA if not already known. Record it here in the plan as `<NEW_SHA>` for the rest of the tasks.

- [ ] **Step 2: Bump the CardCore pin in `Packages/manifest.json`**

Open `Packages/manifest.json`. Current line:

```json
"com.crosshatch.cardcore": "https://github.com/Crosshatch-Games/cardcore.git#main",
```

If pinned to a SHA (e.g. `#05682c93616b`), replace with the new SHA. If pinned to `#main`, force Unity to refresh by deleting `Library/PackageCache/com.crosshatch.cardcore@*` and reopening Unity. Confirm with the user which pin style this project uses; this plan assumes SHA pinning for reproducibility.

- [ ] **Step 3: Verify the new commands are present in the package cache**

After Unity refreshes the package, run:

```bash
ls Library/PackageCache/com.crosshatch.cardcore@*/Runtime/Commands/
```

Expected: lists `DiscardCommand.cs`, `DestroyCardCommand.cs`, `MoveDiscardToDeckCommand.cs`, `ShuffleDeckCommand.cs` alongside the existing `DrawCardCommand.cs`, `PlayCardCommand.cs`, `StartGameCommand.cs`.

Also verify peek accessors:

```bash
grep -n "GetDeckCount\|GetDiscardCount" Library/PackageCache/com.crosshatch.cardcore@*/Runtime/GameEngine.cs
```

Expected: both methods present as public on `GameEngine` (and on `IGameEngine`).

And state shape:

```bash
grep -n "DiscardPile" Library/PackageCache/com.crosshatch.cardcore@*/Runtime/PlayerState.cs
```

Expected: `DiscardPile` property is declared on `PlayerState`.

If any check fails, stop. The engine PR is incomplete — file a follow-up issue and pause C.3 client work.

- [ ] **Step 4: Run existing tests against the new pin**

Open Unity. Run the existing edit-mode test suite (Window → General → Test Runner → EditMode → Run All). All 41 pre-existing tests must pass against the new pin. If any fail, the engine PR has introduced a breaking change; resolve before continuing.

- [ ] **Step 5: Stage and verify**

```bash
git add Packages/manifest.json
git status
```

Stop and await user commit.

---

## Task 1: `DiscardFate` enum + `DrawOutcome` enum

**Files:**
- Create: `Assets/Code/Scripts/GameRules/DiscardFate.cs`
- Create: `Assets/Code/Scripts/GameRules/DrawOutcome.cs`

Pure value types. No tests — these are enums; they're tested transitively via the parser tests (Task 2) and `EndTurn` tests (Task 6).

- [ ] **Step 1: Create `DiscardFate.cs`**

Create `Assets/Code/Scripts/GameRules/DiscardFate.cs` with EXACTLY this content:

```csharp
namespace Code.Scripts.Rules
{
    public enum DiscardFate
    {
        Discard = 0,
        Destroy = 1,
        Upgrade = 2,
    }
}
```

Integer values are explicit per the project memory rule about Unity enum integer serialization — although `DiscardFate` is not currently a `[SerializeField]`, fixing the integer values upfront prevents future serialization breakage if it ever becomes one.

- [ ] **Step 2: Create `DrawOutcome.cs`**

Create `Assets/Code/Scripts/GameRules/DrawOutcome.cs` with EXACTLY this content:

```csharp
namespace Code.Scripts.Rules
{
    public enum DrawOutcome
    {
        Drew = 0,
        NoCardsLeft = 1,
    }
}
```

- [ ] **Step 3: Build**

In Unity, wait for recompile. Expected: no errors. The enums are isolated; compile must succeed cleanly.

- [ ] **Step 4: Stage**

```bash
git add Assets/Code/Scripts/GameRules/DiscardFate.cs Assets/Code/Scripts/GameRules/DiscardFate.cs.meta Assets/Code/Scripts/GameRules/DrawOutcome.cs Assets/Code/Scripts/GameRules/DrawOutcome.cs.meta
git status
```

Stop and await user commit.

---

## Task 2: `DiscardFateParser` with full TDD coverage

**Files:**
- Create: `Assets/Code/Scripts/GameRules/DiscardFateParser.cs`
- Create: `Assets/Tests/EditMode/DiscardFateParserTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/EditMode/DiscardFateParserTests.cs` with EXACTLY this content:

```csharp
using Code.Scripts.Rules;
using NUnit.Framework;

namespace Tests.EditMode
{
    public sealed class DiscardFateParserTests
    {
        [Test]
        public void Parse_Discard_ReturnsDiscard()
        {
            Assert.AreEqual(DiscardFate.Discard, DiscardFateParser.Parse("discard"));
        }

        [Test]
        public void Parse_Destroy_ReturnsDestroy()
        {
            Assert.AreEqual(DiscardFate.Destroy, DiscardFateParser.Parse("destroy"));
        }

        [Test]
        public void Parse_Upgrade_ReturnsUpgrade()
        {
            Assert.AreEqual(DiscardFate.Upgrade, DiscardFateParser.Parse("upgrade"));
        }

        [Test]
        public void Parse_MixedCase_NormalizesToLowerInvariant()
        {
            Assert.AreEqual(DiscardFate.Destroy, DiscardFateParser.Parse("Destroy"));
            Assert.AreEqual(DiscardFate.Upgrade, DiscardFateParser.Parse("UPGRADE"));
        }

        [Test]
        public void Parse_Null_DefaultsToDiscard()
        {
            Assert.AreEqual(DiscardFate.Discard, DiscardFateParser.Parse(null));
        }

        [Test]
        public void Parse_Empty_DefaultsToDiscard()
        {
            Assert.AreEqual(DiscardFate.Discard, DiscardFateParser.Parse(""));
        }

        [Test]
        public void Parse_Whitespace_DefaultsToDiscard()
        {
            Assert.AreEqual(DiscardFate.Discard, DiscardFateParser.Parse("   "));
        }

        [Test]
        public void Parse_UnknownValue_DefaultsToDiscard()
        {
            Assert.AreEqual(DiscardFate.Discard, DiscardFateParser.Parse("explode"));
        }
    }
}
```

- [ ] **Step 2: Run tests; verify they fail**

Open Unity → Test Runner → EditMode → run `DiscardFateParserTests`.
Expected: all 8 fail (compile error — `DiscardFateParser` doesn't exist yet, or all tests fail with type-not-found).

- [ ] **Step 3: Create `DiscardFateParser.cs`**

Create `Assets/Code/Scripts/GameRules/DiscardFateParser.cs` with EXACTLY this content:

```csharp
using UnityEngine;

namespace Code.Scripts.Rules
{
    public static class DiscardFateParser
    {
        public static DiscardFate Parse(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return DiscardFate.Discard;
            }

            switch (raw.ToLowerInvariant())
            {
                case "discard": return DiscardFate.Discard;
                case "destroy": return DiscardFate.Destroy;
                case "upgrade": return DiscardFate.Upgrade;
                default:
                    Debug.LogWarning(
                        $"DiscardFateParser: unknown skipFate '{raw}'. Defaulting to Discard.");
                    return DiscardFate.Discard;
            }
        }
    }
}
```

Note the `UnityEngine` import for `Debug.LogWarning`. The parser is not a `MonoBehaviour`; it's a static helper that lives in the asmdef which already references `UnityEngine` for other reasons.

- [ ] **Step 4: Run tests; verify they pass**

Re-run `DiscardFateParserTests`.
Expected: all 8 pass. The whitespace and empty cases hit the early return; the unknown case logs a warning (NUnit will not fail on `Debug.LogWarning`).

- [ ] **Step 5: Stage**

```bash
git add Assets/Code/Scripts/GameRules/DiscardFateParser.cs Assets/Code/Scripts/GameRules/DiscardFateParser.cs.meta Assets/Tests/EditMode/DiscardFateParserTests.cs Assets/Tests/EditMode/DiscardFateParserTests.cs.meta
git status
```

Stop and await user commit.

---

## Task 3: Card JSON migration — add `"skipFate"` to three test-coverage cards

**Files:**
- Modify: `Assets/StreamingAssets/Cards/wood_resource.json`
- Modify: `Assets/StreamingAssets/Cards/marsh.json`
- Modify: `Assets/StreamingAssets/Cards/water_resource.json`

These three are the test-coverage triplet: one Discard (explicit), one Destroy, one Upgrade. The remaining six cards omit `skipFate`, exercising the default-on-absent path.

- [ ] **Step 1: Edit `wood_resource.json` to add `"skipFate": "discard"`**

Read current contents. Expected shape:

```json
{
  "id": "wood_resource",
  "types": ["resource"],
  "rewards": [
    { "amount": 1, "type": "wood" }
  ],
  "actions": [
    { "verb": "grant_currency", "payload": {} }
  ]
}
```

Replace with:

```json
{
  "id": "wood_resource",
  "types": ["resource"],
  "skipFate": "discard",
  "rewards": [
    { "amount": 1, "type": "wood" }
  ],
  "actions": [
    { "verb": "grant_currency", "payload": {} }
  ]
}
```

- [ ] **Step 2: Edit `marsh.json` to add `"skipFate": "destroy"`**

Read current contents. Add `"skipFate": "destroy"` as a top-level field between `"types"` and `"costs"` (or before `"actions"` if no costs). Expected end state similar to:

```json
{
  "id": "marsh",
  "types": ["piece"],
  "skipFate": "destroy",
  "costs": [
    { "amount": 1, "type": "water" }
  ],
  "actions": [
    { "verb": "pay_cost", "payload": {} },
    { "verb": "spawn_piece", "payload": {} }
  ]
}
```

(Use the actual current `marsh.json` shape — read it first to confirm. Don't change costs or actions.)

- [ ] **Step 3: Edit `water_resource.json` to add `"skipFate": "upgrade"`**

Mirror the wood edit, with `"skipFate": "upgrade"`:

```json
{
  "id": "water_resource",
  "types": ["resource"],
  "skipFate": "upgrade",
  "rewards": [
    { "amount": 1, "type": "water" }
  ],
  "actions": [
    { "verb": "grant_currency", "payload": {} }
  ]
}
```

- [ ] **Step 4: Validate JSON**

Run:

```bash
for f in Assets/StreamingAssets/Cards/wood_resource.json Assets/StreamingAssets/Cards/marsh.json Assets/StreamingAssets/Cards/water_resource.json; do python3 -m json.tool "$f" > /dev/null && echo "$f OK" || echo "$f BAD"; done
```

Expected: three "OK" lines.

- [ ] **Step 5: Stage**

```bash
git add Assets/StreamingAssets/Cards/wood_resource.json Assets/StreamingAssets/Cards/marsh.json Assets/StreamingAssets/Cards/water_resource.json
git status
```

Stop and await user commit.

---

## Task 4: `CardJsonMetadata` loader (client-side skipFate parse)

This task creates a client-side parser for the `skipFate` top-level JSON field. CardCore's `CardDefinition` doesn't carry this metadata (it's a client concern), so we re-parse the card JSON files into a `Dictionary<string, DiscardFate>` keyed by card id at load time.

**Files:**
- Create: `Assets/Code/Scripts/GameRules/CardJsonMetadata.cs`
- Create: `Assets/Tests/EditMode/SkipFateJsonContractTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/EditMode/SkipFateJsonContractTests.cs` with EXACTLY this content:

```csharp
using Code.Scripts.Rules;
using NUnit.Framework;

namespace Tests.EditMode
{
    public sealed class SkipFateJsonContractTests
    {
        [Test]
        public void LoadFromJson_CardWithSkipFateDiscard_ReturnsDiscard()
        {
            const string json = @"{ ""id"": ""x"", ""skipFate"": ""discard"" }";
            var metadata = CardJsonMetadata.LoadFromJsonArray("[" + json + "]");
            Assert.AreEqual(DiscardFate.Discard, metadata.GetSkipFate("x"));
        }

        [Test]
        public void LoadFromJson_CardWithSkipFateDestroy_ReturnsDestroy()
        {
            const string json = @"{ ""id"": ""x"", ""skipFate"": ""destroy"" }";
            var metadata = CardJsonMetadata.LoadFromJsonArray("[" + json + "]");
            Assert.AreEqual(DiscardFate.Destroy, metadata.GetSkipFate("x"));
        }

        [Test]
        public void LoadFromJson_CardWithSkipFateUpgrade_ReturnsUpgrade()
        {
            const string json = @"{ ""id"": ""x"", ""skipFate"": ""upgrade"" }";
            var metadata = CardJsonMetadata.LoadFromJsonArray("[" + json + "]");
            Assert.AreEqual(DiscardFate.Upgrade, metadata.GetSkipFate("x"));
        }

        [Test]
        public void LoadFromJson_CardWithoutSkipFate_DefaultsToDiscard()
        {
            const string json = @"{ ""id"": ""x"" }";
            var metadata = CardJsonMetadata.LoadFromJsonArray("[" + json + "]");
            Assert.AreEqual(DiscardFate.Discard, metadata.GetSkipFate("x"));
        }

        [Test]
        public void LoadFromJson_CardWithUnknownSkipFate_DefaultsToDiscard()
        {
            const string json = @"{ ""id"": ""x"", ""skipFate"": ""explode"" }";
            var metadata = CardJsonMetadata.LoadFromJsonArray("[" + json + "]");
            Assert.AreEqual(DiscardFate.Discard, metadata.GetSkipFate("x"));
        }

        [Test]
        public void GetSkipFate_UnknownCardId_DefaultsToDiscard()
        {
            var metadata = CardJsonMetadata.LoadFromJsonArray("[]");
            Assert.AreEqual(DiscardFate.Discard, metadata.GetSkipFate("nonexistent"));
        }

        [Test]
        public void LoadFromJsonArray_MultipleCards_ParsesEach()
        {
            const string json = @"[
                { ""id"": ""a"", ""skipFate"": ""destroy"" },
                { ""id"": ""b"", ""skipFate"": ""upgrade"" },
                { ""id"": ""c"" }
            ]";
            var metadata = CardJsonMetadata.LoadFromJsonArray(json);
            Assert.AreEqual(DiscardFate.Destroy, metadata.GetSkipFate("a"));
            Assert.AreEqual(DiscardFate.Upgrade, metadata.GetSkipFate("b"));
            Assert.AreEqual(DiscardFate.Discard, metadata.GetSkipFate("c"));
        }
    }
}
```

- [ ] **Step 2: Run tests; verify they fail**

Test Runner → EditMode → `SkipFateJsonContractTests`.
Expected: compile errors — `CardJsonMetadata` doesn't exist.

- [ ] **Step 3: Create `CardJsonMetadata.cs`**

Create `Assets/Code/Scripts/GameRules/CardJsonMetadata.cs` with EXACTLY this content:

```csharp
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace Code.Scripts.Rules
{
    public sealed class CardJsonMetadata
    {
        private readonly IReadOnlyDictionary<string, DiscardFate> _skipFates;

        private CardJsonMetadata(IReadOnlyDictionary<string, DiscardFate> skipFates)
        {
            _skipFates = skipFates;
        }

        public DiscardFate GetSkipFate(string cardId)
        {
            return _skipFates.TryGetValue(cardId, out var fate) ? fate : DiscardFate.Discard;
        }

        public static CardJsonMetadata LoadFromDirectory(string directory)
        {
            var fates = new Dictionary<string, DiscardFate>();
            foreach (var path in Directory.EnumerateFiles(directory, "*.json"))
            {
                var raw = File.ReadAllText(path);
                ParseInto(raw, fates);
            }
            return new CardJsonMetadata(fates);
        }

        public static CardJsonMetadata LoadFromJsonArray(string json)
        {
            var fates = new Dictionary<string, DiscardFate>();
            ParseInto(json, fates);
            return new CardJsonMetadata(fates);
        }

        private static void ParseInto(string raw, Dictionary<string, DiscardFate> sink)
        {
            var token = JToken.Parse(raw);
            if (token is JArray array)
            {
                foreach (var item in array)
                {
                    ExtractSingle(item, sink);
                }
            }
            else if (token is JObject obj)
            {
                ExtractSingle(obj, sink);
            }
        }

        private static void ExtractSingle(JToken card, Dictionary<string, DiscardFate> sink)
        {
            var id = (string)card["id"];
            if (string.IsNullOrEmpty(id))
            {
                return;
            }
            var rawFate = (string)card["skipFate"];
            sink[id] = DiscardFateParser.Parse(rawFate);
        }
    }
}
```

- [ ] **Step 4: Run tests; verify they pass**

Re-run `SkipFateJsonContractTests`.
Expected: all 7 pass.

- [ ] **Step 5: Stage**

```bash
git add Assets/Code/Scripts/GameRules/CardJsonMetadata.cs Assets/Code/Scripts/GameRules/CardJsonMetadata.cs.meta Assets/Tests/EditMode/SkipFateJsonContractTests.cs Assets/Tests/EditMode/SkipFateJsonContractTests.cs.meta
git status
```

Stop and await user commit.

---

## Task 5: `HandCard` DTO grows `SkipFate`

**Files:**
- Modify: `Assets/Code/Scripts/GameRules/HandCard.cs`
- Create: `Assets/Tests/EditMode/HandCardSkipFateTests.cs`

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/EditMode/HandCardSkipFateTests.cs` with EXACTLY this content:

```csharp
using System;
using System.Collections.Generic;
using Code.Scripts.Rules;
using NUnit.Framework;

namespace Tests.EditMode
{
    public sealed class HandCardSkipFateTests
    {
        private static readonly IReadOnlyList<(CurrencyId Id, int Amount)> Empty
            = Array.Empty<(CurrencyId, int)>();

        [Test]
        public void Ctor_StoresSkipFate()
        {
            var card = new HandCard("x", Empty, Empty, isAffordable: true, skipFate: DiscardFate.Destroy);
            Assert.AreEqual(DiscardFate.Destroy, card.SkipFate);
        }

        [Test]
        public void Ctor_DefaultSkipFate_IsDiscard()
        {
            // Verifies the ctor's default parameter rather than the parser fallback.
            var card = new HandCard("x", Empty, Empty, isAffordable: true);
            Assert.AreEqual(DiscardFate.Discard, card.SkipFate);
        }

        [Test]
        public void Ctor_PreservesExistingFields()
        {
            var costs = new[] { (CurrencyId.Stone, 1) };
            var rewards = new[] { (CurrencyId.Wood, 2) };
            var card = new HandCard("x", costs, rewards, isAffordable: false, skipFate: DiscardFate.Upgrade);
            Assert.AreEqual("x", card.CardId);
            Assert.AreEqual(1, card.Costs.Count);
            Assert.AreEqual(1, card.Rewards.Count);
            Assert.IsFalse(card.IsAffordable);
            Assert.AreEqual(DiscardFate.Upgrade, card.SkipFate);
        }
    }
}
```

- [ ] **Step 2: Run tests; verify they fail**

Test Runner → EditMode → `HandCardSkipFateTests`.
Expected: compile errors — `HandCard` has no `SkipFate` and no fifth ctor parameter.

- [ ] **Step 3: Update `HandCard.cs`**

Replace `Assets/Code/Scripts/GameRules/HandCard.cs` with EXACTLY this content:

```csharp
using System.Collections.Generic;

namespace Code.Scripts.Rules
{
    public sealed class HandCard
    {
        public string CardId { get; }
        public IReadOnlyList<(CurrencyId Id, int Amount)> Costs { get; }
        public IReadOnlyList<(CurrencyId Id, int Amount)> Rewards { get; }
        public bool IsAffordable { get; }
        public DiscardFate SkipFate { get; }

        public HandCard(
            string cardId,
            IReadOnlyList<(CurrencyId Id, int Amount)> costs,
            IReadOnlyList<(CurrencyId Id, int Amount)> rewards,
            bool isAffordable,
            DiscardFate skipFate = DiscardFate.Discard)
        {
            CardId = cardId;
            Costs = costs;
            Rewards = rewards;
            IsAffordable = isAffordable;
            SkipFate = skipFate;
        }
    }
}
```

The optional `skipFate` parameter with a default of `Discard` preserves the existing single call site at `GameRules.cs:141` without requiring a same-task GameRules edit. The next task wires up the actual lookup.

- [ ] **Step 4: Run tests; verify they pass**

Re-run `HandCardSkipFateTests`.
Expected: all 3 pass.

Also re-run any pre-existing tests that touch `HandCard`. The two `HandCardRewardsTests` should still pass (they test CardCore JSON contract, not the DTO ctor).

- [ ] **Step 5: Stage**

```bash
git add Assets/Code/Scripts/GameRules/HandCard.cs Assets/Tests/EditMode/HandCardSkipFateTests.cs Assets/Tests/EditMode/HandCardSkipFateTests.cs.meta
git status
```

Stop and await user commit.

---

## Task 6: GameRules — fields, metadata wiring, `GetHand` populates `SkipFate`

This task does the smallest possible GameRules edit that lets `HandCard.SkipFate` come from JSON. It renames `_initialHandSize → _openingHandSize` and `_copiesPerCard → _deckCopiesPerCardDefinition` (both with `[FormerlySerializedAs]`), loads `CardJsonMetadata` in `Awake`, and threads `SkipFate` into the `GetHand` DTO construction. **No `EndTurn` yet** — that's Task 8.

**Files:**
- Modify: `Assets/Code/Scripts/GameRules/GameRules.cs`

- [ ] **Step 1: Confirm the test bar pre-change**

Run all edit-mode tests. Record the green count (should be the existing 41 + the tests added in Tasks 2, 4, 5 = ~57). Any drop after this task indicates a regression.

- [ ] **Step 2: Edit `GameRules.cs` — field renames and metadata load**

Read `Assets/Code/Scripts/GameRules/GameRules.cs` first to get the exact current contents. Make these edits:

**Edit A.** Replace the `using` block at the top (lines 1-7) with:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using CardCore;
using CardCore.Catalog;
using CardCore.Commands;
using UnityEngine;
using UnityEngine.Serialization;
```

(Adds `UnityEngine.Serialization` for `FormerlySerializedAsAttribute`.)

**Edit B.** Replace the `[SerializeField] private int _initialHandSize = 5;` block (around line 17-18) with:

```csharp
        [SerializeField]
        [FormerlySerializedAs("_initialHandSize")]
        private int _openingHandSize = 5;
```

**Edit C.** Replace the `[SerializeField] private int _copiesPerCard = 40;` block (around line 20-21) with:

```csharp
        [SerializeField]
        [FormerlySerializedAs("_copiesPerCard")]
        private int _deckCopiesPerCardDefinition = 2;
```

**Edit D.** Add a new private field next to `_catalog`:

```csharp
        private CardJsonMetadata _cardMetadata;
```

**Edit E.** In `Awake()`, after the existing `_catalog = CardCatalogLoader.LoadFromDirectory(cardsDir);` line, add:

```csharp
            _cardMetadata = CardJsonMetadata.LoadFromDirectory(cardsDir);
```

**Edit F.** In `StartLevel()`, replace both occurrences of `_copiesPerCard` with `_deckCopiesPerCardDefinition` and both occurrences of `_initialHandSize` with `_openingHandSize`.

**Edit G.** In `GetHand()`, replace the `result.Add(new HandCard(instance.DefinitionId, costs, rewards, affordable));` line with:

```csharp
                var skipFate = _cardMetadata.GetSkipFate(instance.DefinitionId);
                result.Add(new HandCard(instance.DefinitionId, costs, rewards, affordable, skipFate));
```

- [ ] **Step 3: Build**

Wait for Unity to recompile. Expected: zero errors.

- [ ] **Step 4: Verify no regressions in test suite**

Test Runner → EditMode → Run All.
Expected: same green count as Step 1.

- [ ] **Step 5: Manual scene check (deck size will drop)**

Open `BlankLevel.unity` and Play.
Expected: opening hand has 5 cards (preserved via `FormerlySerializedAs`); the deck is much smaller (was 360, now 18 — won't be visible without a deck-count HUD, but reshuffling will trigger sooner).
**If the opening hand is empty or wrong size:** the Inspector value didn't migrate; re-save the scene with the GameRules component selected to lock in the rename.

- [ ] **Step 6: Stage**

```bash
git add Assets/Code/Scripts/GameRules/GameRules.cs
git status
```

Stop and await user commit.

---

## Task 7: Deck construction test — `_deckCopiesPerCardDefinition` is honored

**Files:**
- Create: `Assets/Tests/EditMode/DeckConstructionTests.cs`

This is a small parameterized test that pins the deck-size formula. Doesn't require driving a `GameRules` MonoBehaviour — it tests the formula directly via a focused helper. The simplest path: extract the deck-building inner loop to a static helper on `GameRules` (or to a new `DeckBuilder` static class) and test that.

Per the spec architecture, the formula is small enough that the test can simply assert the public `StartLevel` outcome via a `[UnityTest]` play-mode harness — but that requires scene setup. To keep this slice edit-mode-only, extract the formula.

- [ ] **Step 1: Extract `BuildStartingDeck` to a static helper**

Create `Assets/Code/Scripts/GameRules/DeckBuilder.cs` with EXACTLY this content:

```csharp
using System.Collections.Generic;
using CardCore;
using CardCore.Catalog;

namespace Code.Scripts.Rules
{
    public static class DeckBuilder
    {
        public static List<CardInstance> BuildStartingDeck(CardCatalog catalog, int copiesPerCardDefinition)
        {
            var deck = new List<CardInstance>(catalog.Count * copiesPerCardDefinition);
            foreach (var def in catalog.Definitions)
            {
                for (int i = 0; i < copiesPerCardDefinition; i++)
                {
                    deck.Add(CardInstance.From(def));
                }
            }
            return deck;
        }
    }
}
```

- [ ] **Step 2: Update `GameRules.StartLevel` to call the helper**

In `Assets/Code/Scripts/GameRules/GameRules.cs`, replace the existing deck construction (the `var deck = new List<CardInstance>...` block and surrounding `foreach`/`for` loop in `StartLevel()`) with:

```csharp
            var deck = DeckBuilder.BuildStartingDeck(_catalog, _deckCopiesPerCardDefinition);
```

- [ ] **Step 3: Write the failing test**

Create `Assets/Tests/EditMode/DeckConstructionTests.cs` with EXACTLY this content:

```csharp
using CardCore.Catalog;
using Code.Scripts.Rules;
using NUnit.Framework;

namespace Tests.EditMode
{
    public sealed class DeckConstructionTests
    {
        private const string ThreeCardCatalogJson = @"[
            { ""id"": ""a"" },
            { ""id"": ""b"" },
            { ""id"": ""c"" }
        ]";

        [TestCase(1, 3)]
        [TestCase(2, 6)]
        [TestCase(5, 15)]
        public void BuildStartingDeck_HonorsCopiesPerDefinition(int copies, int expectedCount)
        {
            var catalog = CardCatalogLoader.LoadFromJson(ThreeCardCatalogJson);
            var deck = DeckBuilder.BuildStartingDeck(catalog, copies);
            Assert.AreEqual(expectedCount, deck.Count);
        }

        [Test]
        public void BuildStartingDeck_TwoCopies_HasTwoOfEachDefinition()
        {
            var catalog = CardCatalogLoader.LoadFromJson(ThreeCardCatalogJson);
            var deck = DeckBuilder.BuildStartingDeck(catalog, copies: 2);
            int countA = 0, countB = 0, countC = 0;
            foreach (var card in deck)
            {
                if (card.DefinitionId == "a") countA++;
                else if (card.DefinitionId == "b") countB++;
                else if (card.DefinitionId == "c") countC++;
            }
            Assert.AreEqual(2, countA);
            Assert.AreEqual(2, countB);
            Assert.AreEqual(2, countC);
        }
    }
}
```

- [ ] **Step 4: Run tests; verify they pass**

Test Runner → EditMode → `DeckConstructionTests`.
Expected: all 4 pass.

- [ ] **Step 5: Stage**

```bash
git add Assets/Code/Scripts/GameRules/DeckBuilder.cs Assets/Code/Scripts/GameRules/DeckBuilder.cs.meta Assets/Code/Scripts/GameRules/GameRules.cs Assets/Tests/EditMode/DeckConstructionTests.cs Assets/Tests/EditMode/DeckConstructionTests.cs.meta
git status
```

Stop and await user commit.

---

## Task 8: `GameRules.TryDraw` — reshuffle loop with full TDD coverage

This task adds the `TryDraw` helper and pins its reshuffle ordering behavior. `EndTurn` is built on top of `TryDraw` in Task 9.

**Files:**
- Modify: `Assets/Code/Scripts/GameRules/GameRules.cs`
- Create: `Assets/Tests/EditMode/TryDrawReshuffleTests.cs`

`TryDraw` is private on `GameRules`. Test access via `InternalsVisibleTo` is heavy for this slice; instead, the plan introduces a small test-shaped exposure:

- `GameRules` gets a `public DrawOutcome TryDrawForTests(int playerIndex)` method, marked with an XML doc string explicitly noting it's for tests only. Production code uses the private path via `EndTurn` (Task 9).

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/EditMode/TryDrawReshuffleTests.cs` with EXACTLY this content:

```csharp
using System.Collections.Generic;
using System.IO;
using CardCore;
using CardCore.Catalog;
using CardCore.Commands;
using Code.Scripts.Rules;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    // Drives the TryDraw reshuffle loop through GameRules' test-only exposure.
    // GameRules must be live (MonoBehaviour) — constructed via AddComponent on
    // a transient GameObject for the test. The test does NOT call StartLevel;
    // it manually constructs engine state via a helper to control deck and
    // discard contents precisely.
    public sealed class TryDrawReshuffleTests
    {
        private GameObject _go;
        private GameRules _rules;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("GameRulesTestHost");
            _rules = _go.AddComponent<GameRules>();
            _rules.AwakeForTests();   // exposes Awake; see Step 3 production change.
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void TryDraw_EmptyDeckAndDiscard_FiresOnDrawFailed_ReturnsNoCardsLeft()
        {
            _rules.StartLevelForTests(
                catalogJson: @"[{ ""id"": ""x"" }]",
                copiesPerCardDefinition: 1,
                openingHandSize: 1);

            // Opening hand drew the only card. Deck and discard are both empty now.
            int failedCount = 0;
            int playerArg = -1;
            _rules.OnDrawFailed += p => { failedCount++; playerArg = p; };

            var outcome = _rules.TryDrawForTests(0);

            Assert.AreEqual(DrawOutcome.NoCardsLeft, outcome);
            Assert.AreEqual(1, failedCount);
            Assert.AreEqual(0, playerArg);
        }

        [Test]
        public void TryDraw_EmptyDeckWithDiscard_FiresStartingThenCompleted()
        {
            // 2-card deck, opening hand of 2 — deck now empty. Discard one
            // card to populate the discard pile, then TryDraw forces reshuffle.
            _rules.StartLevelForTests(
                catalogJson: @"[{ ""id"": ""x"" }]",
                copiesPerCardDefinition: 2,
                openingHandSize: 2);

            _rules.DiscardHandIndexForTests(0, handIndex: 0);

            bool startingFired = false;
            bool completedFired = false;
            int orderStarting = -1, orderCompleted = -1;
            int sequence = 0;
            _rules.OnReshuffleStarting += p => { startingFired = true; orderStarting = ++sequence; };
            _rules.OnReshuffleCompleted += p => { completedFired = true; orderCompleted = ++sequence; };

            var outcome = _rules.TryDrawForTests(0);

            Assert.AreEqual(DrawOutcome.Drew, outcome);
            Assert.IsTrue(startingFired, "OnReshuffleStarting did not fire");
            Assert.IsTrue(completedFired, "OnReshuffleCompleted did not fire");
            Assert.Less(orderStarting, orderCompleted, "Starting must fire before Completed");
        }

        [Test]
        public void TryDraw_DeckHasCards_DrawsWithoutReshuffle()
        {
            _rules.StartLevelForTests(
                catalogJson: @"[{ ""id"": ""x"" }]",
                copiesPerCardDefinition: 3,
                openingHandSize: 1);
            // 2 cards still in deck after opening hand.

            bool startingFired = false;
            _rules.OnReshuffleStarting += _ => startingFired = true;

            var outcome = _rules.TryDrawForTests(0);

            Assert.AreEqual(DrawOutcome.Drew, outcome);
            Assert.IsFalse(startingFired, "Reshuffle must not fire when deck has cards");
        }

        [Test]
        public void TryDraw_EmptyDeckWithEmptyDiscard_DoesNotFireReshuffleStarting()
        {
            _rules.StartLevelForTests(
                catalogJson: @"[{ ""id"": ""x"" }]",
                copiesPerCardDefinition: 1,
                openingHandSize: 1);
            // Opening hand drew the only card. Discard pile is empty.

            bool startingFired = false;
            _rules.OnReshuffleStarting += _ => startingFired = true;

            _rules.TryDrawForTests(0);

            Assert.IsFalse(startingFired, "Reshuffle must not start when discard is empty");
        }
    }
}
```

- [ ] **Step 2: Run tests; verify they fail**

Test Runner → EditMode → `TryDrawReshuffleTests`.
Expected: compile errors (the test-only helpers don't exist) or runtime failures.

- [ ] **Step 3: Add the production methods and test-only exposures to `GameRules.cs`**

Open `Assets/Code/Scripts/GameRules/GameRules.cs`. Make these edits.

**Edit A.** Add the four new events near the existing `OnStateChanged` declaration (right after line 32):

```csharp
        public event System.Action<int> OnTurnEnded;
        public event System.Action<int> OnReshuffleStarting;
        public event System.Action<int> OnReshuffleCompleted;
        public event System.Action<int> OnDrawFailed;
```

**Edit B.** Add the `_currentTurn` field next to `_placedPieces`:

```csharp
        private int _currentTurn = 1;
```

**Edit C.** Add public accessors near `GetCurrencies()`:

```csharp
        public int CurrentTurn => _currentTurn;
        public int DeckCount => _engine?.GetDeckCount(0) ?? 0;
        public int DiscardCount => _engine?.GetDiscardCount(0) ?? 0;
```

**Edit D.** In `StartLevel()`, ensure `_currentTurn = 1;` is set after `_engine.ExecuteCommand(new StartGameCommand(...));`:

```csharp
            _currentTurn = 1;
```

**Edit E.** Add the private `TryDraw` and the test-only exposure at the bottom of the class (before the closing brace):

```csharp
        private DrawOutcome TryDraw(int playerIndex)
        {
            if (_engine.GetDeckCount(playerIndex) == 0)
            {
                if (_engine.GetDiscardCount(playerIndex) == 0)
                {
                    OnDrawFailed?.Invoke(playerIndex);
                    return DrawOutcome.NoCardsLeft;
                }
                OnReshuffleStarting?.Invoke(playerIndex);
                _engine.ExecuteCommand(new MoveDiscardToDeckCommand(playerIndex));
                _engine.ExecuteCommand(new ShuffleDeckCommand(playerIndex));
                OnReshuffleCompleted?.Invoke(playerIndex);
                if (_engine.GetDeckCount(playerIndex) == 0)
                {
                    OnDrawFailed?.Invoke(playerIndex);
                    return DrawOutcome.NoCardsLeft;
                }
            }
            _engine.ExecuteCommand(new DrawCardCommand(playerIndex));
            return DrawOutcome.Drew;
        }

        // Test-only exposures. Production code does not call these.
        internal void AwakeForTests() => Awake();
        internal DrawOutcome TryDrawForTests(int playerIndex) => TryDraw(playerIndex);
        internal void StartLevelForTests(string catalogJson, int copiesPerCardDefinition, int openingHandSize)
        {
            _catalog = CardCatalogLoader.LoadFromJson(catalogJson);
            _cardMetadata = CardJsonMetadata.LoadFromJsonArray(catalogJson);
            _deckCopiesPerCardDefinition = copiesPerCardDefinition;
            _openingHandSize = openingHandSize;

            var deck = DeckBuilder.BuildStartingDeck(_catalog, _deckCopiesPerCardDefinition);
            _engine.ExecuteCommand(new StartGameCommand(deck, playerCount: 1, seed: 12345));

            if (_startingResources != null)
            {
                foreach (var s in _startingResources.Starting)
                {
                    _currencies.Add(s.Id, s.Amount);
                }
            }

            for (int i = 0; i < _openingHandSize; i++)
            {
                _engine.ExecuteCommand(new DrawCardCommand(playerId: 0));
            }
            _currentTurn = 1;
        }
        internal void DiscardHandIndexForTests(int playerIndex, int handIndex)
        {
            var state = _engine.GetCurrentState();
            var card = state.Players[playerIndex].Hand.Cards[handIndex];
            _engine.ExecuteCommand(new DiscardCommand(playerIndex, card.InstanceId));
        }
```

The `internal` modifiers require the production asmdef to grant access to the test asmdef. Add `[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Tests.EditMode")]` — this needs an `AssemblyInfo.cs` in the production asmdef folder. See Edit F.

**Edit F.** Create `Assets/Code/Scripts/GameRules/AssemblyInfo.cs` with EXACTLY this content:

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Tests.EditMode")]
```

This grants the test assembly access to `internal` members of `Code.Scripts.Rules`.

- [ ] **Step 4: Verify the test asmdef name matches**

Read `Assets/Tests/EditMode/Tests.EditMode.asmdef`. Confirm `"name": "Tests.EditMode"`. This must match the string in `AssemblyInfo.cs` exactly.

- [ ] **Step 5: Build**

Wait for recompile. Expected: zero errors. If `DiscardCommand`, `MoveDiscardToDeckCommand`, `ShuffleDeckCommand`, or `GetDeckCount` are missing, the engine PR pin in Task 0 is incomplete — stop and resolve.

- [ ] **Step 6: Run tests; verify they pass**

Test Runner → EditMode → `TryDrawReshuffleTests`.
Expected: all 4 pass.

Re-run the full suite. Expected: no regressions.

- [ ] **Step 7: Stage**

```bash
git add Assets/Code/Scripts/GameRules/GameRules.cs Assets/Code/Scripts/GameRules/AssemblyInfo.cs Assets/Code/Scripts/GameRules/AssemblyInfo.cs.meta Assets/Tests/EditMode/TryDrawReshuffleTests.cs Assets/Tests/EditMode/TryDrawReshuffleTests.cs.meta
git status
```

Stop and await user commit.

---

## Task 9: `GameRules.EndTurn()` — full implementation with TDD coverage

**Files:**
- Modify: `Assets/Code/Scripts/GameRules/GameRules.cs`
- Create: `Assets/Tests/EditMode/EndTurnTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/EditMode/EndTurnTests.cs` with EXACTLY this content:

```csharp
using CardCore.Commands;
using Code.Scripts.Rules;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public sealed class EndTurnTests
    {
        private GameObject _go;
        private GameRules _rules;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("GameRulesTestHost");
            _rules = _go.AddComponent<GameRules>();
            _rules.AwakeForTests();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void EndTurn_IncrementsCurrentTurn()
        {
            _rules.StartLevelForTests(
                catalogJson: @"[{ ""id"": ""x"" }]",
                copiesPerCardDefinition: 5,
                openingHandSize: 1);
            Assert.AreEqual(1, _rules.CurrentTurn);

            _rules.EndTurn();

            Assert.AreEqual(2, _rules.CurrentTurn);
        }

        [Test]
        public void EndTurn_FiresOnTurnEnded_WithNewTurnNumber()
        {
            _rules.StartLevelForTests(
                catalogJson: @"[{ ""id"": ""x"" }]",
                copiesPerCardDefinition: 5,
                openingHandSize: 1);
            int receivedTurn = -1;
            _rules.OnTurnEnded += t => receivedTurn = t;

            _rules.EndTurn();

            Assert.AreEqual(2, receivedTurn);
        }

        [Test]
        public void EndTurn_DiscardFate_MovesCardToDiscardPile()
        {
            _rules.StartLevelForTests(
                catalogJson: @"[{ ""id"": ""disc"", ""skipFate"": ""discard"" }]",
                copiesPerCardDefinition: 5,
                openingHandSize: 1);
            int discardBefore = _rules.DiscardCount;

            _rules.EndTurn();

            Assert.AreEqual(discardBefore + 1, _rules.DiscardCount);
        }

        [Test]
        public void EndTurn_DestroyFate_DoesNotIncreaseDiscardPile()
        {
            _rules.StartLevelForTests(
                catalogJson: @"[{ ""id"": ""des"", ""skipFate"": ""destroy"" }]",
                copiesPerCardDefinition: 5,
                openingHandSize: 1);
            int discardBefore = _rules.DiscardCount;

            _rules.EndTurn();

            Assert.AreEqual(discardBefore, _rules.DiscardCount,
                "Destroyed cards must not enter the discard pile.");
        }

        [Test]
        public void EndTurn_UpgradeFate_TreatedAsDiscard()
        {
            // C.3 deferral: Upgrade is plumbed through to the dispatch site but
            // resolved as Discard, with a TODO marker for C.4.
            _rules.StartLevelForTests(
                catalogJson: @"[{ ""id"": ""upg"", ""skipFate"": ""upgrade"" }]",
                copiesPerCardDefinition: 5,
                openingHandSize: 1);
            int discardBefore = _rules.DiscardCount;

            _rules.EndTurn();

            Assert.AreEqual(discardBefore + 1, _rules.DiscardCount);
        }

        [Test]
        public void EndTurn_DrawsNewHandOfOpeningSize()
        {
            _rules.StartLevelForTests(
                catalogJson: @"[
                    { ""id"": ""a"" }, { ""id"": ""b"" }, { ""id"": ""c"" },
                    { ""id"": ""d"" }, { ""id"": ""e"" }, { ""id"": ""f"" },
                    { ""id"": ""g"" }, { ""id"": ""h"" }
                ]",
                copiesPerCardDefinition: 2,
                openingHandSize: 3);
            // Opening hand of 3 drawn from 16-card deck.

            _rules.EndTurn();

            Assert.AreEqual(3, _rules.GetHand().Count);
        }

        [Test]
        public void EndTurn_EmptyHand_StillAdvancesTurn()
        {
            _rules.StartLevelForTests(
                catalogJson: @"[{ ""id"": ""x"" }]",
                copiesPerCardDefinition: 5,
                openingHandSize: 0);
            // Opening hand of 0 — hand is empty going into EndTurn.
            Assert.AreEqual(1, _rules.CurrentTurn);

            _rules.EndTurn();

            Assert.AreEqual(2, _rules.CurrentTurn);
        }

        [Test]
        public void EndTurn_FiresOnStateChangedExactlyOnce()
        {
            _rules.StartLevelForTests(
                catalogJson: @"[{ ""id"": ""x"" }]",
                copiesPerCardDefinition: 5,
                openingHandSize: 2);
            int changedCount = 0;
            _rules.OnStateChanged += () => changedCount++;

            _rules.EndTurn();

            Assert.AreEqual(1, changedCount,
                "EndTurn must fire OnStateChanged once at the end, not per discard.");
        }
    }
}
```

- [ ] **Step 2: Run tests; verify they fail**

Test Runner → EditMode → `EndTurnTests`.
Expected: compile error — `GameRules.EndTurn()` doesn't exist yet.

- [ ] **Step 3: Implement `EndTurn` in `GameRules.cs`**

Add this method to `GameRules.cs` next to `ConfirmPlay`:

```csharp
        public void EndTurn()
        {
            const int p = 0;
            var state = _engine.GetCurrentState();
            var hand = state.Players[p].Hand;

            // Snapshot fates before issuing commands (the hand is about to mutate).
            var fates = new List<(System.Guid InstanceId, DiscardFate Fate)>(hand.Cards.Count);
            foreach (var card in hand.Cards)
            {
                fates.Add((card.InstanceId, _cardMetadata.GetSkipFate(card.DefinitionId)));
            }

            foreach (var (id, fate) in fates)
            {
                switch (fate)
                {
                    case DiscardFate.Discard:
                        _engine.ExecuteCommand(new DiscardCommand(p, id));
                        break;
                    case DiscardFate.Destroy:
                        _engine.ExecuteCommand(new DestroyCardCommand(p, id));
                        break;
                    case DiscardFate.Upgrade:
                        // TODO(C.4): real upgrade semantics; treated as Discard for now.
                        _engine.ExecuteCommand(new DiscardCommand(p, id));
                        break;
                }
            }

            for (int i = 0; i < _openingHandSize; i++)
            {
                var outcome = TryDraw(p);
                if (outcome == DrawOutcome.NoCardsLeft)
                {
                    break;
                }
            }

            _currentTurn++;
            OnTurnEnded?.Invoke(_currentTurn);
            OnStateChanged?.Invoke();
        }
```

- [ ] **Step 4: Run tests; verify they pass**

Test Runner → EditMode → `EndTurnTests`.
Expected: all 8 pass.

- [ ] **Step 5: Run full suite for regressions**

Test Runner → EditMode → Run All.
Expected: pre-existing 41 + new tests all pass.

- [ ] **Step 6: Stage**

```bash
git add Assets/Code/Scripts/GameRules/GameRules.cs Assets/Tests/EditMode/EndTurnTests.cs Assets/Tests/EditMode/EndTurnTests.cs.meta
git status
```

Stop and await user commit.

---

## Task 10: `EndTurnButton` scene-side component

**Files:**
- Create: `Assets/Code/Scripts/UI/EndTurnButton.cs`

No tests for the button itself — it's a one-line wire between a `ColliderButton` click and `GameRules.EndTurn()`. The behavior is exercised by Task 9's tests and manual scene check in Task 13.

- [ ] **Step 1: Create `EndTurnButton.cs`**

Create `Assets/Code/Scripts/UI/EndTurnButton.cs` with EXACTLY this content:

```csharp
using Code.Scripts.Rules;
using UnityEngine;

namespace Code.Scripts.UI
{
    public sealed class EndTurnButton : MonoBehaviour
    {
        [SerializeField] private GameRules _gameRules;
        [SerializeField] private ColliderButton _button;

        private void OnEnable()
        {
            _button.OnClick.AddListener(OnClicked);
        }

        private void OnDisable()
        {
            _button.OnClick.RemoveListener(OnClicked);
        }

        private void OnClicked()
        {
            _gameRules.EndTurn();
        }
    }
}
```

`ColliderButton.OnClick` is the existing `UnityEvent` exposed on the existing button component (see `Assets/Code/Scripts/ColliderButton.cs:10`). `UnityEvent.AddListener / RemoveListener` is the standard pattern.

- [ ] **Step 2: Build**

Wait for recompile. Expected: zero errors.

- [ ] **Step 3: Stage**

```bash
git add Assets/Code/Scripts/UI/EndTurnButton.cs Assets/Code/Scripts/UI/EndTurnButton.cs.meta
git status
```

Stop and await user commit.

---

## Task 11: `TurnCounterHUD` scene-side component

**Files:**
- Create: `Assets/Code/Scripts/UI/TurnCounterHUD.cs`

- [ ] **Step 1: Create `TurnCounterHUD.cs`**

Create `Assets/Code/Scripts/UI/TurnCounterHUD.cs` with EXACTLY this content:

```csharp
using Code.Scripts.Rules;
using TMPro;
using UnityEngine;

namespace Code.Scripts.UI
{
    public sealed class TurnCounterHUD : MonoBehaviour
    {
        [SerializeField] private GameRules _gameRules;
        [SerializeField] private TMP_Text _label;

        private void OnEnable()
        {
            _gameRules.OnTurnEnded += Repaint;
            _gameRules.OnStateChanged += RepaintFromCurrent;
            RepaintFromCurrent();
        }

        private void OnDisable()
        {
            _gameRules.OnTurnEnded -= Repaint;
            _gameRules.OnStateChanged -= RepaintFromCurrent;
        }

        private void Repaint(int turn)
        {
            _label.text = $"Turn {turn}";
        }

        private void RepaintFromCurrent()
        {
            _label.text = $"Turn {_gameRules.CurrentTurn}";
        }
    }
}
```

The double subscription is intentional: `OnTurnEnded` is the dedicated push hook; `OnStateChanged` covers the initial paint after `StartLevel` (which fires `OnStateChanged` but not `OnTurnEnded`). `RepaintFromCurrent` is also called once in `OnEnable` so the label is correct even before the first state change.

- [ ] **Step 2: Build**

Wait for recompile. Expected: zero errors.

- [ ] **Step 3: Stage**

```bash
git add Assets/Code/Scripts/UI/TurnCounterHUD.cs Assets/Code/Scripts/UI/TurnCounterHUD.cs.meta
git status
```

Stop and await user commit.

---

## Task 12: Documentation — update `docs/GameRules.md` to reflect C.3 landing

**Files:**
- Modify: `docs/GameRules.md`

`docs/GameRules.md:66` describes the C.3 design as locked. After C.3 lands, that section gets a `Status: Shipped 2026-MM-DD` line so future readers don't think it's pending.

- [ ] **Step 1: Edit `docs/GameRules.md`**

Find the `## Slice C.3 — Skip turn, turn counter, real deck cycle` heading. Immediately after the heading, add a single line:

```markdown
> **Status:** Shipped <YYYY-MM-DD>. See `docs/superpowers/specs/2026-05-30-c3-skip-turn-counter-deck-cycle-design.md`.
```

(Engineer fills in the actual ship date when the branch merges.)

- [ ] **Step 2: Stage**

```bash
git add docs/GameRules.md
git status
```

Stop and await user commit.

---

## Task 13: Scene wiring (manual — Unity Editor)

This task is the user's manual step. The agent cannot reliably edit `.unity` files.

**Files:**
- Modify: `Assets/Scenes/BlankLevel.unity` (user, in-editor)

- [ ] **Step 1: Open `BlankLevel.unity`**

In Unity, open `Assets/Scenes/BlankLevel.unity`.

- [ ] **Step 2: Verify `_openingHandSize` and `_deckCopiesPerCardDefinition` migrated**

Select the `GameRules` GameObject. In the Inspector, confirm:
- `Opening Hand Size` (was `Initial Hand Size`) — value preserved from before.
- `Deck Copies Per Card Definition` (was `Copies Per Card`) — value preserved from before, but the **default in code is now 2**. If the migrated value is `40`, you have a choice: keep it (huge deck, hard to test reshuffle) or change to `2`. Per the spec, set it to `2`.

- [ ] **Step 3: Add the `EndTurnButton` GameObject**

Create a new GameObject under whichever HUD root canvas / 3D HUD layer the project uses (see `Assets/Code/Scripts/UI/CurrencyHUD.cs` placement in the scene for the existing pattern). Name it `EndTurnButton`.

Add components:
1. A `ColliderButton` (existing component; needs a `Collider` to be tappable — a `BoxCollider` is fine).
2. The new `EndTurnButton` script.

In the `EndTurnButton` script's Inspector:
- Drag the scene's `GameRules` into `_gameRules`.
- Drag the same GameObject's own `ColliderButton` into `_button`.

Position it bottom-left of the HUD (per spec). Use a TMP label child or quick mesh visual so it's visible during play test — the exact look is designer-tunable.

Also set the `ColliderButton.Interactable` field to `true`.

Note: `ColliderButton.OnEnable()` references `_inputManager` (`Assets/Code/Scripts/ColliderButton.cs:31`). This is **Pattern B**: `ColliderButton` expects its `Initialize(InputManager)` to be called before `OnEnable`. The existing scene-resident `ColliderButton`s (if any) need verification. Two options:
1. **Preferred for scene-resident HUD buttons:** convert `ColliderButton` to Pattern A by making `_inputManager` a `[SerializeField]`. Out of scope for C.3 — file a follow-up if `EndTurnButton` errors at scene load.
2. **Workaround:** wrap `EndTurnButton` in a runtime-spawned prefab.

**If `ColliderButton` is currently Pattern B only**, the cleanest path is a small follow-up to make `_inputManager` `[SerializeField]` (additive, doesn't break Pattern B usage if `Initialize` still wins). Flag to the user when this task is reached.

- [ ] **Step 4: Add the `TurnCounterHUD` GameObject**

Create a new GameObject under the HUD root. Name it `TurnCounterHUD`.

Add components:
1. A `TMP_Text` (TextMeshPro - Text).
2. The new `TurnCounterHUD` script.

In the script's Inspector:
- Drag the scene's `GameRules` into `_gameRules`.
- Drag the GameObject's own `TMP_Text` into `_label`.

Position top-left of the HUD.

- [ ] **Step 5: Save the scene**

`File → Save` or `Cmd+S`. Saving locks the migrated `[FormerlySerializedAs]` field values into the scene file.

- [ ] **Step 6: Play test**

Press Play. Manual checklist:
- Opening hand of 5 cards visible (default `_openingHandSize`).
- `Turn 1` visible top-left.
- Click `End Turn`. Hand empties, new hand of 5 draws, label becomes `Turn 2`.
- After 1–2 turns, deck empties; reshuffle happens (no visible animation yet, but the new hand still draws).
- After many turns (deck × 2 ÷ hand size ≈ 4 turns of pure discard), if a card flagged `destroy` was in hand, the cycle eventually loses cards from the system. Confirm via `Debug.Log(_rules.DiscardCount + _rules.DeckCount + _rules.GetHand().Count)` if curious; full visibility comes with the deck/discard HUD in a later slice.
- If the `marsh` Piece card was destroyed previously, eventually you can't draw it again. Expected.

- [ ] **Step 7: Stage the scene change**

```bash
git add Assets/Scenes/BlankLevel.unity
git status
```

Stop and await user commit.

---

## Task 14: Final regression sweep + cleanup

**Files:** none (verification + housekeeping)

- [ ] **Step 1: Run the full edit-mode test suite**

Test Runner → EditMode → Run All.

Expected count: 41 pre-existing + 8 `DiscardFateParserTests` + 7 `SkipFateJsonContractTests` + 3 `HandCardSkipFateTests` + 4 `DeckConstructionTests` + 4 `TryDrawReshuffleTests` + 8 `EndTurnTests` = **75 total**.

If any pre-existing test fails, regression. Fix before continuing.

- [ ] **Step 2: Confirm no `TODO(C.3)` markers remain unhandled**

```bash
grep -rn "TODO(C.3)" Assets/Code/ docs/
```

Expected: empty.

(The `TODO(C.4)` markers on `Upgrade` dispatch and the `TODO(C.X scrubber)` on `ConfirmPlay` are intentional and must remain.)

- [ ] **Step 3: Confirm no debug logs in production paths**

```bash
grep -rn "Debug.Log\b" Assets/Code/Scripts/GameRules/ Assets/Code/Scripts/UI/EndTurnButton.cs Assets/Code/Scripts/UI/TurnCounterHUD.cs
```

Expected: only the `Debug.LogWarning` inside `DiscardFateParser`. Anything else is leftover and must be removed.

- [ ] **Step 4: Visual smoke — full turn cycle**

Play `BlankLevel`. Press End Turn at least 6 times in a row without playing any cards. Confirm:
- Hand of 5 cards each turn.
- Turn counter increments each press.
- Eventually `OnDrawFailed` fires (visible only if you add a temporary `Debug.Log` to a `TurnCounterHUD` test subscriber, or if you watch the hand shrink in late turns). Acceptable to skip if the deck composition + destroys never zero out within reasonable play; the unit test pins the behavior.

- [ ] **Step 5: Stop and request review**

```bash
git status
git log --oneline main..HEAD
```

Hand off to user for review before opening a PR.

---

## Self-Review

**1. Spec coverage:**

| Spec section | Task |
| --- | --- |
| Engine prereq (commands, events, state, accessors) | Task 0 verifies; engine PR itself ships separately |
| `DiscardFate` enum | Task 1 |
| `DrawOutcome` enum | Task 1 |
| `DiscardFateParser` | Task 2 |
| Per-card JSON `"skipFate"` + 3 test cards | Task 3 |
| Client-side JSON metadata loader | Task 4 |
| `HandCard.SkipFate` | Task 5 |
| `GameRules._openingHandSize`, `_deckCopiesPerCardDefinition`, `_currentTurn` | Task 6 |
| `GetHand()` populates `SkipFate` | Task 6 |
| `DeckBuilder` extraction + deck size formula tests | Task 7 |
| `TryDraw` reshuffle loop + events | Task 8 |
| `EndTurn()` algorithm | Task 9 |
| `EndTurnButton` scene component | Task 10 |
| `TurnCounterHUD` scene component | Task 11 |
| `docs/GameRules.md` status update | Task 12 |
| Scene wiring (BlankLevel) | Task 13 (manual) |
| Edge cases (empty hand, empty deck+discard, post-reshuffle empty, unknown skipFate, Upgrade-as-Discard) | Covered across Tasks 8, 9 (tests) + Task 2 (parser) |
| Reshuffle order assertion | Task 8 (`TryDraw_EmptyDeckWithDiscard_FiresStartingThenCompleted`) |
| `OnStateChanged` fires once per `EndTurn` | Task 9 (`EndTurn_FiresOnStateChangedExactlyOnce`) |

All spec requirements are covered.

**2. Placeholder scan:** No "TBD", "TODO", "fill in later", or "similar to Task N" in any task. Every step has the actual code, exact paths, and exact commands.

**3. Type consistency:**
- `DiscardFate` — used consistently across Tasks 1, 2, 4, 5, 9.
- `DrawOutcome` — used consistently across Tasks 1, 8, 9.
- `HandCard` ctor parameter name `skipFate` — consistent in Task 5 (definition) and Task 6 (call site).
- `GameRules.OnTurnEnded` signature `Action<int>` — consistent across Task 8 declaration and Tasks 9, 11 consumers.
- `_openingHandSize` / `_deckCopiesPerCardDefinition` field names — consistent across Tasks 6, 7, 8, 9.
- `TryDrawForTests`, `StartLevelForTests`, `DiscardHandIndexForTests`, `AwakeForTests` — declared in Task 8, used in Tasks 8 and 9 tests.

**4. Ambiguity check:** One known unknown is documented inline (Task 13 Step 3 — `ColliderButton`'s `_inputManager` Pattern B vs A). It's flagged for the engineer to resolve at scene-wiring time with a documented preferred fix.

---

## Out of scope (per spec)

- Per-card fate icons in the UI.
- Per-turn player-chosen fates.
- Real `Upgrade` semantics.
- Win/lose wiring on `OnDrawFailed`.
- C.2b scrub-safety fix.
- Engine-side reshuffle policy.
- Deck / discard count HUDs (designed but deferred to a later visual pass).
