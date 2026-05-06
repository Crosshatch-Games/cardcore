---
name: cardcore-card-system
description: Use when implementing the rich card system slice in the CardCore repo — building CardDefinition, CardInstance, CardCatalog, CardCatalogLoader, the Cardcore Markdown parser, Action, IRuleset, IActionHandler, ActionDispatcher, or migrating existing Card references to CardInstance
---

# CardCore — Rich Card System Slice

## Overview

This slice replaces the walking-skeleton's minimal `Card` (`{ Id, Name }`) with a rich, designer-authorable card system: `CardDefinition` (immutable, JSON-loaded) and `CardInstance` (mutable, in-game), the Cardcore Markdown text format, the `CardCatalog` and loader, and the `IRuleset` / `IActionHandler` extension points by which a future ruleset will interpret cards.

**No concrete ruleset is built in this slice.** `IRuleset` is an empty marker. The deck-builder ruleset lives in another project and another slice.

## Read first (in this order)

1. **`docs~/superpowers/specs/2026-05-04-card-system-design.md`** — the approved spec for this slice. Every type, signature, and validation rule is in there. If the spec and this skill ever disagree, the spec wins.
2. **`Documentation~/Claude MD - Cardcore Cards.md`** — the user's card concept doc. Source of truth for the Cardcore Markdown notation.
3. **`Documentation~/Card Data - Heterogenous_card_list.csv`** — sample data, designer authoring artifact only. CardCore does NOT read CSV. Use it to sanity-check that your data model can express what designers actually write.
4. **`cardcore-conventions` skill (this repo)** — the encapsulation, Unity-compat, and architecture rules. Mandatory before writing any code.

## Required background skills

- **`superpowers:test-driven-development`** — strict TDD per step. Red → green → refactor. No production code without a failing test.
- **`superpowers:verification-before-completion`** — before claiming "done", run `dotnet test` from the repo root and confirm the integration test passes. `dotnet build` succeeding is not enough.

## Implementation order

Build in this order. Each step is its own TDD cycle (write tests → red → minimal implementation → green → refactor).

1. **`CurrencyAmount`** — `readonly record struct (int Amount, string Type)`. Validate `Type` non-empty in constructor. Tests: value semantics, type validation, zero/negative amounts allowed.
2. **`Action`** — `sealed record { string Verb; JObject Payload }`. Tests: verb non-empty validation, empty `{}` payload allowed, null/array/scalar payload rejected.
3. **`MarkdownToken` hierarchy + `MarkdownText` + `MarkdownParser`** — table-driven parser tests covering every grammar row in the spec, plus all edge cases (`+4 [points]`, `${percent}`, unbalanced brackets, empty input, `#if(night)`).
4. **`CardDefinition`** — sealed record. Only `Id` is required. Every other field defaults to empty/null. Tests: id validation (lowercase, no whitespace, non-empty), every-field-empty case, immutability.
5. **`CardInstance`** — sealed class. `From(definition)` factory. Internal mutation methods. `[JsonConstructor]` for round-trip from event log. Tests: factory copies all fields, mutation methods are `internal` (test via `[InternalsVisibleTo]`), JSON round-trip preserves state.
6. **`CardCatalog`** — sealed class. Two constructors (with and without warnings). Tests: duplicate-id rejection, lookup semantics, `Get` throws on miss, `TryGet` returns false on miss.
7. **`CardCatalogLoader`** — static class. `LoadFromDirectory` / `LoadFromJson` / `LoadFromStream` / `LoadDefinition`. Tests: directory-of-files, single array file, aggregate error reporting (load fails, exception lists every bad card), warnings collection (unpaired amount/type), per-card validation rules.
8. **`IRuleset` (empty marker) + `IActionHandler` + `ActionDispatcher`** — Tests: dispatcher registration, throw on duplicate verb (use `Assert.Throws<InvalidOperationException>`), throw on unknown verb dispatch, dispatch invokes correct handler with correct args.
9. **Migration — replace `Card` with `CardInstance`.** Delete `Runtime/Models/Card.cs`. Update `Deck`, `Hand`, `GameState.PlayArea`, `GameStarted.InitialDeckOrder`, `CardDrawn` (`CardId: int` → `InstanceId: Guid`), `CardPlayed` (same), `StartGameCommand` (takes `IReadOnlyList<CardInstance>`). Rewrite every test in `Tests~/PureCSharp/` that references `Card`. Update `Documentation~/unity-client.md` in the same change.
10. **Integration test** — load a small catalog from JSON, build a deck of `CardInstance`s, run Start/Draw/Play, assert the event log round-trips through `JsonConvert.SerializeObject(..., GameEvent.JsonSettings)` and replays to identical state.

**Do not skip ahead.** Each step's tests catch the next step's typos. Skipping leaves a debugging swamp.

## Hard rules from the spec (do not violate)

- **`CardDefinition.Id` is the only required field.** Every other property has an empty/null default. JSON missing-field produces the empty default, not an exception.
- **`CardDefinition` is immutable.** `CardInstance` is mutable but only via `internal` methods.
- **`IRuleset` stays empty.** Do not add methods. The deck-builder slice will drive what gets added — guessing now is wrong.
- **Duplicate verb registration on `ActionDispatcher` throws** `InvalidOperationException`. Last-wins is wrong.
- **`StartGameCommand` takes a pre-built `IReadOnlyList<CardInstance>`**, not a catalog + ids. Host owns deck composition.
- **Currency types are open-ended strings.** Only the (amount, type) pair format is validated; pairing-warnings are warnings (collected on `CardCatalog.LoadWarnings`), not errors.
- **Newtonsoft.Json everywhere.** No `System.Text.Json`. (Repeats `cardcore-conventions`, but the temptation to "just use the modern API" is real — don't.)
- **`InstanceId` is `Guid`.** Don't use sequential ints. A deck of 10 Coppers needs 10 distinct identifiers without coordination.
- **No card scripts.** The doc mentions "complicated cards have a script file." That mechanism is deferred. `CardDefinition` carries no `ScriptId` field in this slice.
- **No `CardCore.CSV` namespace.** CardCore does not read CSV. The CSV in `Documentation~/` is a designer artifact.

## Public API surface (added by this slice)

These types are public and must appear in `Documentation~/unity-client.md` after migration:

- `CardDefinition`, `CardInstance`, `CurrencyAmount`, `Action`
- `MarkdownText`, `MarkdownToken` and all subtypes (`LiteralToken`, `IconToken`, `KeywordToken`, `VariableToken`, `TypeRefToken`)
- `MarkdownParser` (`Parse`, `TryParse`)
- `CardCatalog`, `CardCatalogLoader`, `CardCatalogLoadException`
- `IRuleset`, `IActionHandler`, `ActionDispatcher`

These types are removed:

- `Card` (deleted entirely; no shim)

These signatures change:

- `Deck`, `Hand`, `GameState.PlayArea`, `GameStarted.InitialDeckOrder` — element type is now `CardInstance`
- `CardDrawn`, `CardPlayed` — `CardId: int` → `InstanceId: Guid`
- `StartGameCommand` — accepts `IReadOnlyList<CardInstance>`

## Test fixtures

Create under `Tests~/PureCSharp/Fixtures/Cards/`:

- `valid_minimal.json` — `{"id":"x"}` and nothing else. Tests the "only id required" promise.
- `valid_full.json` — every field populated, drawn from a real card in the CSV (e.g., `Reverie Muse`).
- `invalid_missing_id.json` — `{"name":{"raw":"x","tokens":[]}}`. Loader must reject.
- `invalid_uppercase_id.json` — `{"id":"Foo"}`. Loader must reject.
- `invalid_whitespace_id.json` — `{"id":"foo bar"}`. Loader must reject.
- `invalid_action_no_verb.json` — Action with empty verb. Loader must reject.
- `warnings_unpaired_cost.json` — Cost with amount but no type, or vice versa. Loader returns catalog with `LoadWarnings` populated.

Add the fixtures directory to the test project's csproj as `CopyToOutputDirectory="PreserveNewest"` so tests can read them.

## Done when

- All new types implemented, each with passing tests
- `Card.cs` deleted; all references migrated to `CardInstance`
- All migrated tests passing (`Tests~/PureCSharp/` runs green)
- Integration test passing
- `Documentation~/unity-client.md` updated to reflect the new public surface
- `docs~/superpowers/specs/2026-05-04-card-system-design.md` Status updated from `Approved` to `Implemented`
- `dotnet test` from repo root exits 0
- (Optional, for the user) Unity import smoke-tested — flagged in handoff if not done

## Common mistakes

| Mistake | Fix |
|---|---|
| Made `CardDefinition.Name` a `string` "for simplicity" | The spec requires `MarkdownText`. Real titles use icons (`[red sails]`). Plain string forces every consumer to reinvent parsing. |
| Used `int CardId` somewhere instead of `Guid InstanceId` in events | Sequential ids force coordination across hosts and break instance identity when cards are copied/cloned. The spec is `Guid InstanceId`. |
| `IRuleset` has methods on it | Empty marker. Add nothing. The deck-builder slice drives method addition. |
| `ActionDispatcher.Register` last-wins | Throws on duplicate. Misregistration must fail loudly. |
| `StartGameCommand` takes `CardCatalog catalog, IReadOnlyList<string> ids` | Wrong. Takes `IReadOnlyList<CardInstance>`. Host builds the deck. |
| Built a CSV reader | Out of scope. CSV is designer-authoring only. JSON is the canonical format. |
| Added `CardDefinition.ScriptId` for "future flexibility" | Deferred. Add it in the slice that needs it. YAGNI. |
| Skipped updating `unity-client.md` | Same change, not a follow-up. The doc is what every consumer reads. |
| Kept `Card` as a deprecation shim | No back-compat shims. Delete it. CLAUDE.md is explicit. |
| `MarkdownParser` errors on unknown character classes | Don't. Anything not matching a token rule becomes a `LiteralToken`. Only `[`/`]` mismatch and `${` without `}` are errors. |
| Used `System.Text.Json` for `JObject` | Use `Newtonsoft.Json.Linq.JObject`. The spec is explicit. |

## Red flags — STOP

- About to add a method to `IRuleset`
- About to use `int` for an instance identifier
- About to write a CSV reader
- About to make `CardDefinition` mutable
- About to use `System.Text.Json` for the action payload
- About to skip the migration step ("I'll do that later")
- About to skip updating `unity-client.md` ("I'll do docs at the end")
- About to write code without a failing test (see `superpowers:test-driven-development`)
- About to claim "done" without `dotnet test` passing (see `superpowers:verification-before-completion`)

If any of these is true: stop, re-read the spec or the relevant skill, and pick the conforming path.
