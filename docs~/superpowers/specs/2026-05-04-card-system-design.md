# Card System — Design Spec

**Date:** 2026-05-04
**Status:** Implemented
**Slice:** Rich card data model + catalog + Cardcore Markdown + ruleset extension points

## Purpose

Replace the walking skeleton's minimal `Card` (`{ Id, Name }`) with a rich, designer-authorable card system that can express the full range of card data found in real card games (currencies, costs, rewards, thresholds, actions, targets, rarity, flavor, icon-rich text). Establish the Card-Definition vs. Card-Instance split, the Cardcore Markdown text format, the catalog and loader, and the extension points by which a future Ruleset will interpret cards. No concrete ruleset is built in this slice.

## Scope

### In scope

- `CardDefinition` (immutable catalog entry) and `CardInstance` (mutable in-game card) as separate types
- `CardCatalog` — in-memory collection of definitions, indexed by id
- `CardCatalogLoader` — convenience helper that reads JSON from a directory, string, or stream
- Cardcore Markdown parser — turns text fields into a structured `MarkdownToken` stream
- `Action` data type — `{ Verb, Payload }`, opaque to CardCore
- `IRuleset` — empty marker interface
- `IActionHandler` — extension point for action verb dispatch
- `ActionDispatcher` — verb registry; throws on duplicate registration
- Migration: existing `Card` references in `GameEngine`, `GameState`, events, commands, models, and tests are replaced with `CardInstance`. `Card` is deleted.

### Out of scope (deferred)

- Any concrete `IRuleset` implementation. The deck-builder ruleset is a separate slice in another project.
- Card scripts. `CardDefinition` carries no script reference; the doc's "complicated cards have a script file" mechanism is deferred until a real card needs it.
- Currency-type vocabulary validation. Type strings are open-ended; CardCore only validates the (amount, type) pair format and warns on unpaired entries.
- Variable substitution. `VariableToken("cost1")` parses as data; resolution is a ruleset concern.
- Turn lifecycle, scoring, game-over detection, networking, AI, simulation runner, board, UI/visualizers/scrubber. Untouched from current state.
- Effect resolution. CardCore provides the action *dispatch* mechanism; nothing in CardCore *resolves* what a card does — that's ruleset territory.

## Non-Goals

- This slice is **not** playable as a game. It supplies the data layer and extension points; gameplay rules come with the deck-builder migration.
- This slice does **not** define what any keyword, verb, currency type, or target identifier *means*. Those are ruleset-specific. CardCore only validates structure.
- This slice does **not** read CSV. The supplied `Card Data - Heterogenous_card_list.csv` is a designer authoring artifact, not a CardCore input. JSON is the canonical storage format.

## Architecture

CardCore stays a pure C# library, no Unity dependencies, packaged as a Unity Package via Git URL. Event sourcing is unchanged: commands → events → state.

The card system layers cleanly on top:

- **`CardDefinition`** is content. Loaded from JSON via `CardCatalogLoader`, lives in a `CardCatalog`, immutable for the lifetime of the game.
- **`CardInstance`** is in-game state. Created from a `CardDefinition` via `CardInstance.From(definition)`. Held by `Deck`, `Hand`, and `GameState.PlayArea`. Mutation is `internal` — exposed only to assemblies declared in `[InternalsVisibleTo]` (the future ruleset assembly and tests).
- **Cardcore Markdown** is the structured text format used in Name, Flavor, Targets, and Action descriptions. The parser tokenizes raw strings at catalog load time. Both the raw string and the parsed token stream are retained.
- **`Action`** is opaque to CardCore: a string verb and a Newtonsoft `JObject` payload. Rulesets register `IActionHandler` instances per verb with an `ActionDispatcher` they own. CardCore does not invoke handlers; rulesets do.
- **`IRuleset`** is an empty marker. The first concrete ruleset (deck-builder, in a separate slice) will drive what methods get added.

**Determinism is preserved.** `GameStarted.InitialDeckOrder` becomes `IReadOnlyList<CardInstance>` — the post-shuffle order of instances. Replay reads instances verbatim from the event log; no RNG re-runs.

**No public mutable state.** Per `CLAUDE.md` and `cardcore-conventions`: all fields private/private-readonly, all access via properties (mostly `init`-only), constructors fully initialize, types `sealed` by default. `CardInstance` mutation methods are `internal`.

## Components

### Data model

#### `CardDefinition` (immutable)

```csharp
public sealed record CardDefinition
{
    public string Id { get; init; }                         // ONLY required field
    public MarkdownText Name { get; init; }                 // default: MarkdownText.Empty
    public IReadOnlyList<string> Types { get; init; }       // default: empty
    public IReadOnlyList<CurrencyAmount> Costs { get; init; }
    public IReadOnlyList<CurrencyAmount> Rewards { get; init; }
    public IReadOnlyList<CurrencyAmount> Thresholds { get; init; }
    public IReadOnlyList<Action> Actions { get; init; }
    public IReadOnlyList<MarkdownText> Targets { get; init; }
    public string? Back { get; init; }                      // back-face id, nullable
    public string? Rarity { get; init; }                    // open-ended, nullable
    public MarkdownText Flavor { get; init; }               // default: MarkdownText.Empty
}
```

- Only `Id` is required. Validated lowercase, no whitespace, non-empty.
- Every other field has an empty/null default. JSON missing-field produces the empty default, not an error.
- `Name`, `Flavor`, individual `Targets` entries are `MarkdownText` because real titles use icons (`[red sails]`). Targets in the sample CSV are short phrases (`"empty selected tile"`, `"empty tableau slot"`); using `MarkdownText` is consistent and adds zero overhead — most target entries will be a single `LiteralToken`.
- `Types` is plain `string` list — types are simple identifiers.
- Sealed record. JSON-deserializable via Newtonsoft.

#### `CardInstance` (mutable, in-game)

```csharp
public sealed class CardInstance
{
    public Guid InstanceId { get; }                         // unique per instance, runtime-generated
    public string DefinitionId { get; }                     // points back to catalog
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

    // Mutation methods (internal — ruleset only via [InternalsVisibleTo]):
    internal void AddAction(int index, Action action);
    internal void RemoveAction(int index);
    internal void ReplaceAction(int index, Action action);
    internal void SetCost(int index, CurrencyAmount cost);
    // Minimal set; grow as concrete rulesets demand.
}
```

- `InstanceId` is `Guid` so a deck of 10 Coppers is 10 distinct instances pointing at definition `"copper"`.
- Properties expose `IReadOnlyList<>` views over private `List<>` backing fields.
- Mutation surface is `internal` per CLAUDE.md ("mutating operations live as methods on the owning class").
- **JSON round-trip:** `CardInstance` is serialized into the event log (`GameStarted.InitialDeckOrder`, etc.). The `[JsonConstructor]` constructor accepts every backing field directly (matching `Hand`/`Deck`/`Player` pattern) so deserialization restores the full instance state — including any post-creation ruleset mutations. The `From(definition)` factory is the only construction path for fresh instances.

#### `CurrencyAmount`

```csharp
public readonly record struct CurrencyAmount(int Amount, string Type);
```

- `Type` validated non-empty. `Amount` unrestricted (zero and negative are both legal — rulesets may have meaning for them).

#### `Action`

```csharp
public sealed record Action
{
    public string Verb { get; init; }                       // non-empty
    public JObject Payload { get; init; }                   // Newtonsoft JObject
}
```

- Validation at catalog load: verb non-empty, payload is a valid JSON object (not null, array, or scalar). Empty object `{}` is allowed — the verb may not need parameters.

#### `MarkdownText` and `MarkdownToken`

```csharp
public sealed record MarkdownText
{
    public static readonly MarkdownText Empty = new("", Array.Empty<MarkdownToken>());
    public string Raw { get; init; }
    public IReadOnlyList<MarkdownToken> Tokens { get; init; }
}

public abstract record MarkdownToken;
public sealed record LiteralToken(string Text) : MarkdownToken;
public sealed record IconToken(string Id) : MarkdownToken;                       // [some_id]
public sealed record KeywordToken(string Id, string? Param) : MarkdownToken;     // #foo, #if(night)
public sealed record VariableToken(string Name) : MarkdownToken;                 // ${name}
public sealed record TypeRefToken(string Category, string Value) : MarkdownToken;// category:value (in type contexts)
```

### Cardcore Markdown grammar

| Pattern | Token | Notes |
|---|---|---|
| `[id]` | `IconToken("id")` | `id` may contain spaces and underscores. Brackets must balance. |
| `#id` | `KeywordToken("id", null)` | `id` ends at whitespace, punctuation, or `(`. |
| `#id(param)` | `KeywordToken("id", "param")` | Param can contain anything except `)`. |
| `${name}` | `VariableToken("name")` | `name` ends at `}`. |
| `cat:val` | `TypeRefToken("cat", "val")` | Only valid in fields where type-refs are allowed (Types and Threshold Type at the structured level). In free text, `:` is literal. |
| anything else | `LiteralToken(text)` | Whitespace and punctuation stay attached. Consecutive literal characters merge. |

Edge cases:

- Mixed runs: `"+4 [points]"` → `[Literal("+4 "), Icon("points")]`
- Quoted flavor: `$"{percent}% of cards"` keeps the surrounding `$"` and `"` as literals; `${percent}` is a variable token. Consumers strip quotes if they want.
- Empty input: `""` → `MarkdownText.Empty`
- Unbalanced bracket: load-time error with file/field context
- Unknown character classes never error — they become literal

#### Parser API

```csharp
namespace CardCore.Markdown;

public static class MarkdownParser
{
    public static MarkdownText Parse(string raw);
    public static bool TryParse(string raw, out MarkdownText result, out string? error);
}
```

Pure static, no state.

### Catalog

#### `CardCatalog`

```csharp
public sealed class CardCatalog
{
    public CardCatalog(IEnumerable<CardDefinition> definitions);   // throws on duplicate Id; LoadWarnings empty
    public CardCatalog(IEnumerable<CardDefinition> definitions, IReadOnlyList<string> loadWarnings); // used by loader

    public int Count { get; }
    public IReadOnlyCollection<CardDefinition> Definitions { get; }
    public IReadOnlyList<string> LoadWarnings { get; }             // populated by loader; empty otherwise
    public CardDefinition Get(string id);                          // throws KeyNotFoundException
    public bool TryGet(string id, out CardDefinition def);
    public bool Contains(string id);
}
```

- Immutable after construction.
- Both constructors validate: no duplicate ids.
- `Get` throws on miss; `TryGet` for lookup-or-fallback.
- `LoadWarnings` is populated by the loader-aware constructor (used by `CardCatalogLoader`); the single-arg constructor leaves it empty.

#### `CardCatalogLoader`

```csharp
namespace CardCore.Catalog;

public static class CardCatalogLoader
{
    public static CardCatalog LoadFromDirectory(string directoryPath);
    public static CardCatalog LoadFromJson(string json);
    public static CardCatalog LoadFromStream(Stream stream);
    public static CardDefinition LoadDefinition(JObject json);     // lower-level
}
```

Behavior:

- Newtonsoft.Json throughout.
- All three top-level methods return a fully-validated `CardCatalog`. Validation runs *before* the catalog is constructed — if any card fails, the whole load fails with an aggregate `CardCatalogLoadException` listing every failing card and its error.
- A directory may contain one definition per file (`copper.json`) or arrays per file (`treasures.json` with `[{...}, {...}]`). Both shapes work.
- Per-card validation:
  1. `Id` present, lowercase, no whitespace
  2. All markdown text fields parse without error
  3. All `Action.Verb` non-empty, `Action.Payload` is a JSON object
  4. All `CurrencyAmount.Type` non-empty
- Warnings (do not halt load):
  - Cost / Reward / Threshold amount appears without a paired type, or vice versa (per the doc's "warn if not paired" guidance)

### Ruleset extension points

#### `IRuleset` (marker)

```csharp
namespace CardCore;

public interface IRuleset
{
    // Empty. Slot for future ruleset-driven methods.
    // First concrete ruleset (deck-builder) will drive what gets added here.
}
```

#### `IActionHandler`

```csharp
namespace CardCore;

public interface IActionHandler
{
    string Verb { get; }
    IReadOnlyList<GameEvent> Handle(Action action, CardInstance card, GameState state);
}
```

- Returns `IReadOnlyList<GameEvent>` matching the `IGameCommand.Execute` convention.
- Handler deserializes `action.Payload` into its own typed shape.

#### `ActionDispatcher`

```csharp
namespace CardCore;

public sealed class ActionDispatcher
{
    public ActionDispatcher();
    public void Register(IActionHandler handler);                      // throws on duplicate verb
    public bool IsRegistered(string verb);
    public IReadOnlyList<GameEvent> Dispatch(Action action, CardInstance card, GameState state);
    // Dispatch throws InvalidOperationException if no handler is registered for action.Verb.
}
```

- Owned by the ruleset, not by `GameEngine`. Engine stays ignorant of action semantics.

## Migration

The walking skeleton uses `Card` (`{ Id, Name }`) throughout. This slice replaces it with `CardInstance` and deletes `Card`. No back-compat shim per CLAUDE.md.

| Current | Replacement |
|---|---|
| `Runtime/Models/Card.cs` | **Deleted.** |
| `Deck` holds `List<Card>` | `Deck` holds `List<CardInstance>` |
| `Hand` holds `List<Card>` | `Hand` holds `List<CardInstance>` |
| `GameState.PlayArea : IReadOnlyList<Card>` | `IReadOnlyList<CardInstance>` |
| `GameStarted.InitialDeckOrder : IReadOnlyList<Card>` | `IReadOnlyList<CardInstance>` |
| `CardDrawn.CardId : int` | `CardDrawn.InstanceId : Guid` |
| `CardPlayed.CardId : int` | `CardPlayed.InstanceId : Guid` |
| `StartGameCommand(IReadOnlyList<Card> deck, ...)` | `StartGameCommand(IReadOnlyList<CardInstance> deck, ...)` — host pre-builds instances from the catalog |

`StartGameCommand`'s deck input is `IReadOnlyList<CardInstance>` (Option A): the host owns deck composition (which definitions, how many copies, in what order). This keeps `StartGameCommand` ruleset-agnostic.

What stays the same:

- Event-sourcing architecture
- `IGameEngine` method signatures (the *types* in those signatures change, the surface does not)
- Determinism (post-shuffle order captured in `GameStarted`)
- JSON polymorphism via `[JsonDerivedType]` on `GameEvent`

`Documentation~/unity-client.md` is updated in the same change to reflect the new `Card`-related API surface.

## Testing strategy

Same model as the walking skeleton, extended. xUnit project at `Tests/PureCSharp/`. Strict TDD per `superpowers:test-driven-development`.

New test files:

- `CardDefinitionTests.cs` — Id validation, empty-field defaults, immutability
- `CardInstanceTests.cs` — `From(definition)` copy semantics, internal mutation surface
- `CurrencyAmountTests.cs` — value semantics, type validation
- `ActionTests.cs` — verb/payload validation
- `MarkdownParserTests.cs` — table-driven against every grammar rule and edge case
- `CardCatalogTests.cs` — duplicate-id rejection, lookup semantics
- `CardCatalogLoaderTests.cs` — directory load, json string load, stream load, aggregate error reporting, warnings collection
- `ActionDispatcherTests.cs` — registration, throw on duplicate, throw on unknown verb, dispatch round-trip

Migrated tests:

- `CardTests.cs` → `CardInstanceTests.cs`
- `DeckTests`, `HandTests`, `PlayerTests`, `GameStateTests`, `GameEngineTests`, all command/event tests rewritten against `CardInstance`

Fixtures:

- `Tests/PureCSharp/Fixtures/Cards/` — `valid_minimal.json` (`{"id":"x"}`), `valid_full.json`, `invalid_*.json`

Integration test:

- One end-to-end test loading a small catalog from JSON, building a deck of instances, running Start/Draw/Play, asserting events round-trip via `JsonConvert.SerializeObject(..., GameEvent.JsonSettings)`.

The empty Unity NUnit assembly at `Tests/Runtime/` stays empty.

## File layout

```
Runtime/
├── Models/
│   ├── CardDefinition.cs       (NEW)
│   ├── CardInstance.cs         (NEW; replaces Card.cs)
│   ├── CurrencyAmount.cs       (NEW)
│   ├── Action.cs               (NEW)
│   ├── Deck.cs                 (modified — holds CardInstance)
│   ├── Hand.cs                 (modified — holds CardInstance)
│   └── Player.cs               (unchanged)
├── Markdown/
│   ├── MarkdownText.cs         (NEW)
│   ├── MarkdownToken.cs        (NEW; abstract + sealed records)
│   └── MarkdownParser.cs       (NEW)
├── Catalog/
│   ├── CardCatalog.cs          (NEW)
│   ├── CardCatalogLoader.cs    (NEW)
│   └── CardCatalogLoadException.cs (NEW)
├── Ruleset/
│   ├── IRuleset.cs             (NEW; empty marker)
│   ├── IActionHandler.cs       (NEW)
│   └── ActionDispatcher.cs     (NEW)
├── Events/                     (modified — CardId → InstanceId)
├── Commands/                   (modified — StartGameCommand takes CardInstance list)
├── GameEngine.cs               (unchanged signature, types in events change)
├── GameEvent.cs                (unchanged)
├── GameState.cs                (modified — PlayArea is IReadOnlyList<CardInstance>)
└── IGameEngine.cs              (unchanged)
```

## Open questions

None remaining. All clarifying questions answered during brainstorming:

1. Scope: rich card data + ruleset extension points (no concrete ruleset)
2. Scripts: deferred — `CardDefinition` carries no script reference
3. Definition vs. Instance: two distinct types
4. Catalog source: convenience loader + accept pre-built catalog
5. Currencies: open-ended strings; pair-format warnings only
6. Markdown ownership: CardCore owns the parser; validation at load time
7. Action shape: opaque `{ Verb, JObject Payload }`
8. `IRuleset`: empty marker; `IActionHandler` is the real extension point
9. Duplicate verb registration: throw
10. `StartGameCommand` deck input: pre-built `CardInstance` list (host owns composition)

## Implementation order

Per `cardcore-card-system` skill (TDD per step):

1. `CurrencyAmount`
2. `Action`
3. `MarkdownToken` hierarchy + `MarkdownText` + `MarkdownParser`
4. `CardDefinition`
5. `CardInstance` + factory + internal mutation methods
6. `CardCatalog`
7. `CardCatalogLoader`
8. `IRuleset` marker + `IActionHandler` + `ActionDispatcher`
9. Migration: replace `Card` with `CardInstance`; delete `Card.cs`; rewrite tests; update `unity-client.md`
10. Integration test: catalog → deck → Start/Draw/Play → JSON round-trip
