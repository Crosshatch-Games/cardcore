# CardCore Walking Skeleton Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the smallest end-to-end event-sourcing loop in CardCore — three commands, three events, JSON polymorphism, replay invariant — with strict TDD throughout.

**Architecture:** Pure C# library. Commands are pure functions of `state → events`. The engine appends events to a log and applies them to a single mutated `GameState`; reads return JSON-clones. `GameEvent` is an abstract `record` with `[JsonDerivedType]` polymorphism via `System.Text.Json`. `GameStarted` carries the post-shuffle deck order (no `System.Random` reliance for replay correctness).

**Tech Stack:** .NET 8, C# 12, `System.Text.Json`, xUnit (`Tests/PureCSharp/`), Unity Package layout (`Runtime/` + `Tests/Runtime/`). User is new to TDD — the first feature narrates the rhythm explicitly; later features narrate more briefly.

**Spec:** `docs/superpowers/specs/2026-04-27-cardcore-walking-skeleton-design.md`

---

## How to use this plan

- Each task is a vertical TDD slice for one production unit. Steps follow strict red → green → refactor.
- Every step is one action (2–5 minutes). Mark `[x]` as you go.
- Commit at the end of each task. Never skip the commit step.
- Run `dotnet test` from `Tests/PureCSharp/` unless told otherwise.
- The TDD-rhythm narration in Task 3 (Card validation) is the teaching example. Re-read it any time the loop feels unclear.

---

## Task 1: Repository bootstrap (no tests, no production code)

This task only sets up files Unity and `dotnet` need to recognize the repo as a package + test project. There is nothing to test yet.

**Files:**
- Create: `.gitignore`
- Create: `package.json`
- Create: `Runtime/CardCore.asmdef`
- Create: `Runtime/CardCore.csproj`
- Create: `Tests/Runtime/CardCore.Tests.asmdef`
- Create: `Tests/PureCSharp/CardCore.PureTests.csproj`
- Create: `CardCore.sln`

- [ ] **Step 1: Write `.gitignore`**

Create `.gitignore`:
```gitignore
# .NET
bin/
obj/
*.user
*.suo
.vs/

# Unity (in case a consumer project syncs into this repo)
[Ll]ibrary/
[Tt]emp/
[Oo]bj/
[Bb]uild/
[Bb]uilds/
[Ll]ogs/
[Mm]emoryCaptures/
[Uu]serSettings/

# IDEs
.idea/
.vscode/
*.swp

# OS
.DS_Store
Thumbs.db
```

- [ ] **Step 2: Write `package.json`**

Create `package.json`:
```json
{
  "name": "com.crosshatch.cardcore",
  "version": "0.0.1",
  "displayName": "CardCore",
  "description": "Headless event-sourced card game engine.",
  "unity": "2022.3",
  "author": {
    "name": "Crosshatch Games"
  }
}
```

- [ ] **Step 3: Write `Runtime/CardCore.asmdef`**

Create `Runtime/CardCore.asmdef`:
```json
{
  "name": "CardCore",
  "rootNamespace": "CardCore",
  "references": [],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": true
}
```

- [ ] **Step 4: Write `Runtime/CardCore.csproj`**

Create `Runtime/CardCore.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>12</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>CardCore</RootNamespace>
    <AssemblyName>CardCore</AssemblyName>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="CardCore.PureTests" />
  </ItemGroup>
</Project>
```

- [ ] **Step 5: Write `Tests/Runtime/CardCore.Tests.asmdef` (placeholder)**

Create `Tests/Runtime/CardCore.Tests.asmdef`:
```json
{
  "name": "CardCore.Tests",
  "rootNamespace": "CardCore.Tests",
  "references": ["CardCore"],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": false,
  "defineConstraints": ["UNITY_INCLUDE_TESTS"],
  "optionalUnityReferences": ["TestAssemblies"],
  "noEngineReferences": false
}
```

- [ ] **Step 6: Write `Tests/PureCSharp/CardCore.PureTests.csproj`**

Create `Tests/PureCSharp/CardCore.PureTests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>12</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>CardCore.PureTests</RootNamespace>
    <AssemblyName>CardCore.PureTests</AssemblyName>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\Runtime\CardCore.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 7: Write `CardCore.sln`**

Run from repo root:
```bash
dotnet new sln -n CardCore
dotnet sln add Runtime/CardCore.csproj
dotnet sln add Tests/PureCSharp/CardCore.PureTests.csproj
```

- [ ] **Step 8: Verify both projects build (no code yet, so they should build empty)**

Run from repo root:
```bash
dotnet build CardCore.sln
```
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- [ ] **Step 9: Verify `dotnet test` runs (zero tests, zero failures)**

Run from repo root:
```bash
dotnet test CardCore.sln
```
Expected: `Passed!  - Failed: 0, Passed: 0, Skipped: 0, Total: 0`.

- [ ] **Step 10: Commit**

```bash
git add .gitignore package.json Runtime/ Tests/ CardCore.sln
git commit -m "Bootstrap CardCore package and test projects"
```

---

## Task 2: First failing test — `Card` constructor accepts valid input

**TDD rhythm narration (read this once; subsequent tasks abbreviate):**

The TDD loop is **red → green → refactor**:

1. **RED** — write a test for behavior that doesn't exist yet. Run it. Watch it fail. The compile-error or assertion-failure is *evidence the test is wired up*.
2. **GREEN** — write the *minimum* production code that makes the test pass. Often laughably trivial. Resist the urge to write "good code" yet — that's the next phase.
3. **REFACTOR** — with green tests as a safety net, clean up. Tests stay green.

The discipline: never write production code without a failing test first. We follow this rigorously through the whole plan.

**Files:**
- Create: `Tests/PureCSharp/CardTests.cs`
- Create: `Runtime/Models/Card.cs`

- [ ] **Step 1: RED — write the failing test**

Create `Tests/PureCSharp/CardTests.cs`:
```csharp
using CardCore;
using Xunit;

namespace CardCore.PureTests;

public class CardTests
{
    [Fact]
    public void Constructor_WithValidInput_SetsProperties()
    {
        var card = new Card(Id: 42, Name: "Copper");

        Assert.Equal(42, card.Id);
        Assert.Equal("Copper", card.Name);
    }
}
```

- [ ] **Step 2: RED — verify it fails to compile (the "test failure" at this stage)**

Run from repo root:
```bash
dotnet test
```
Expected: build error — `error CS0246: The type or namespace name 'Card' could not be found`. **This is the red.**

- [ ] **Step 3: GREEN — write minimal `Card`**

Create `Runtime/Models/Card.cs`:
```csharp
namespace CardCore;

public sealed record Card(int Id, string Name);
```

- [ ] **Step 4: GREEN — run the test, watch it pass**

Run from repo root:
```bash
dotnet test
```
Expected: `Passed: 1, Failed: 0`.

- [ ] **Step 5: Commit**

```bash
git add Tests/PureCSharp/CardTests.cs Runtime/Models/Card.cs
git commit -m "Add Card record with valid construction"
```

---

## Task 3: `Card` validation — reject invalid input

**Files:**
- Modify: `Tests/PureCSharp/CardTests.cs` (add tests)
- Modify: `Runtime/Models/Card.cs` (add validation)

- [ ] **Step 1: RED — add a test for negative id**

Append to `Tests/PureCSharp/CardTests.cs` inside the `CardTests` class:
```csharp
    [Fact]
    public void Constructor_WithNegativeId_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Card(Id: -1, Name: "Copper"));
    }
```

- [ ] **Step 2: RED — verify it fails**

Run: `dotnet test --filter FullyQualifiedName~CardTests.Constructor_WithNegativeId_Throws`
Expected: FAIL — `Assert.Throws() Failure: No exception was thrown`.

- [ ] **Step 3: GREEN — add validation to `Card`**

Replace the contents of `Runtime/Models/Card.cs`:
```csharp
namespace CardCore;

public sealed record Card
{
    public int Id { get; }
    public string Name { get; }

    public Card(int Id, string Name)
    {
        if (Id < 0)
            throw new ArgumentException("Card.Id must be >= 0.", nameof(Id));
        if (string.IsNullOrEmpty(Name))
            throw new ArgumentException("Card.Name must be non-empty.", nameof(Name));
        this.Id = Id;
        this.Name = Name;
    }
}
```

- [ ] **Step 4: GREEN — run the test, watch it pass**

Run: `dotnet test --filter FullyQualifiedName~CardTests`
Expected: `Passed: 2, Failed: 0`.

- [ ] **Step 5: RED — add a test for null name**

Append to `Tests/PureCSharp/CardTests.cs` inside the `CardTests` class:
```csharp
    [Fact]
    public void Constructor_WithNullName_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Card(Id: 1, Name: null!));
    }

    [Fact]
    public void Constructor_WithEmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Card(Id: 1, Name: ""));
    }
```

- [ ] **Step 6: GREEN — run, watch them pass (already implemented)**

Run: `dotnet test --filter FullyQualifiedName~CardTests`
Expected: `Passed: 4, Failed: 0`. (Implementation from step 3 already covers null/empty — confirming the test agrees with our intent without extra production code.)

- [ ] **Step 7: Commit**

```bash
git add Tests/PureCSharp/CardTests.cs Runtime/Models/Card.cs
git commit -m "Add Card constructor validation"
```

---

## Task 4: `Hand` model — minimal API

**Files:**
- Create: `Tests/PureCSharp/HandTests.cs`
- Create: `Runtime/Models/Hand.cs`

- [ ] **Step 1: RED — write tests for empty Hand and Add/Count**

Create `Tests/PureCSharp/HandTests.cs`:
```csharp
using CardCore;
using Xunit;

namespace CardCore.PureTests;

public class HandTests
{
    [Fact]
    public void NewHand_IsEmpty()
    {
        var hand = new Hand();
        Assert.Equal(0, hand.Count);
    }

    [Fact]
    public void Add_IncreasesCount()
    {
        var hand = new Hand();
        hand.Add(new Card(1, "A"));
        hand.Add(new Card(2, "B"));
        Assert.Equal(2, hand.Count);
    }

    [Fact]
    public void Indexer_ReturnsCardAtPosition()
    {
        var hand = new Hand();
        var a = new Card(1, "A");
        var b = new Card(2, "B");
        hand.Add(a);
        hand.Add(b);
        Assert.Equal(a, hand[0]);
        Assert.Equal(b, hand[1]);
    }

    [Fact]
    public void RemoveAt_ReturnsAndRemovesCard()
    {
        var hand = new Hand();
        var a = new Card(1, "A");
        var b = new Card(2, "B");
        hand.Add(a);
        hand.Add(b);

        var removed = hand.RemoveAt(0);

        Assert.Equal(a, removed);
        Assert.Equal(1, hand.Count);
        Assert.Equal(b, hand[0]);
    }

    [Fact]
    public void RemoveAt_OutOfRange_Throws()
    {
        var hand = new Hand();
        Assert.Throws<ArgumentOutOfRangeException>(() => hand.RemoveAt(0));
    }
}
```

- [ ] **Step 2: RED — verify they fail to compile**

Run: `dotnet test --filter FullyQualifiedName~HandTests`
Expected: build error — `Hand` does not exist.

- [ ] **Step 3: GREEN — write minimal `Hand`**

Create `Runtime/Models/Hand.cs`:
```csharp
namespace CardCore;

public sealed class Hand
{
    private readonly List<Card> _cards = new();

    public int Count => _cards.Count;

    public Card this[int index] => _cards[index];

    public void Add(Card card)
    {
        if (card is null) throw new ArgumentNullException(nameof(card));
        _cards.Add(card);
    }

    public Card RemoveAt(int index)
    {
        if (index < 0 || index >= _cards.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        var card = _cards[index];
        _cards.RemoveAt(index);
        return card;
    }
}
```

- [ ] **Step 4: GREEN — run, watch them pass**

Run: `dotnet test --filter FullyQualifiedName~HandTests`
Expected: `Passed: 5, Failed: 0`.

- [ ] **Step 5: Commit**

```bash
git add Tests/PureCSharp/HandTests.cs Runtime/Models/Hand.cs
git commit -m "Add Hand model with add/remove/index"
```

---

## Task 5: `Player` model

**Files:**
- Create: `Tests/PureCSharp/PlayerTests.cs`
- Create: `Runtime/Models/Player.cs`

- [ ] **Step 1: RED — write tests**

Create `Tests/PureCSharp/PlayerTests.cs`:
```csharp
using CardCore;
using Xunit;

namespace CardCore.PureTests;

public class PlayerTests
{
    [Fact]
    public void NewPlayer_HasIdAndEmptyHand()
    {
        var player = new Player(id: 0);
        Assert.Equal(0, player.Id);
        Assert.Equal(0, player.Hand.Count);
    }

    [Fact]
    public void NegativeId_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Player(id: -1));
    }
}
```

- [ ] **Step 2: RED — verify failure**

Run: `dotnet test --filter FullyQualifiedName~PlayerTests`
Expected: build error — `Player` does not exist.

- [ ] **Step 3: GREEN — write `Player`**

Create `Runtime/Models/Player.cs`:
```csharp
namespace CardCore;

public sealed class Player
{
    public int Id { get; }
    public Hand Hand { get; }

    public Player(int id)
    {
        if (id < 0) throw new ArgumentException("Player.Id must be >= 0.", nameof(id));
        Id = id;
        Hand = new Hand();
    }
}
```

- [ ] **Step 4: GREEN — verify pass**

Run: `dotnet test --filter FullyQualifiedName~PlayerTests`
Expected: `Passed: 2, Failed: 0`.

- [ ] **Step 5: Commit**

```bash
git add Tests/PureCSharp/PlayerTests.cs Runtime/Models/Player.cs
git commit -m "Add Player model"
```

---

## Task 6: `Deck` model

`Deck` wraps `List<Card>`, takes a seeded `Random` to shuffle on construction, and exposes `Count`, `RemoveTop()` (returns the card AND its pre-removal index — needed for `CardDrawn.DeckIndexBefore`), and `FindCardById(int)`. Composition is set once.

**Files:**
- Create: `Tests/PureCSharp/DeckTests.cs`
- Create: `Runtime/Models/Deck.cs`

- [ ] **Step 1: RED — write tests**

Create `Tests/PureCSharp/DeckTests.cs`:
```csharp
using CardCore;
using Xunit;

namespace CardCore.PureTests;

public class DeckTests
{
    private static List<Card> ThreeCards() => new()
    {
        new Card(1, "A"),
        new Card(2, "B"),
        new Card(3, "C"),
    };

    [Fact]
    public void NewDeck_HasCountEqualToInputCount()
    {
        var deck = new Deck(ThreeCards(), new Random(0));
        Assert.Equal(3, deck.Count);
    }

    [Fact]
    public void RemoveTop_DecrementsCount_AndReturnsTopCard()
    {
        var deck = new Deck(ThreeCards(), new Random(0));
        var topBefore = deck[0];

        var removed = deck.RemoveTop();

        Assert.Equal(topBefore.Card, removed.Card);
        Assert.Equal(0, removed.IndexBefore);
        Assert.Equal(2, deck.Count);
    }

    [Fact]
    public void RemoveTop_OnEmptyDeck_Throws()
    {
        var deck = new Deck(new List<Card>(), new Random(0));
        Assert.Throws<InvalidOperationException>(() => deck.RemoveTop());
    }

    [Fact]
    public void FindCardById_ReturnsMatch()
    {
        var deck = new Deck(ThreeCards(), new Random(0));
        var card = deck.FindCardById(2);
        Assert.Equal(2, card.Id);
    }

    [Fact]
    public void FindCardById_NoMatch_Throws()
    {
        var deck = new Deck(ThreeCards(), new Random(0));
        Assert.Throws<InvalidOperationException>(() => deck.FindCardById(99));
    }

    [Fact]
    public void Constructor_WithSameSeed_ProducesSameOrder()
    {
        var d1 = new Deck(ThreeCards(), new Random(42));
        var d2 = new Deck(ThreeCards(), new Random(42));
        Assert.Equal(d1[0].Id, d2[0].Id);
        Assert.Equal(d1[1].Id, d2[1].Id);
        Assert.Equal(d1[2].Id, d2[2].Id);
    }

    [Fact]
    public void Constructor_NullCards_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new Deck(null!, new Random(0)));
    }
}
```

- [ ] **Step 2: RED — verify failure**

Run: `dotnet test --filter FullyQualifiedName~DeckTests`
Expected: build errors — `Deck` does not exist.

- [ ] **Step 3: GREEN — write `Deck`**

Create `Runtime/Models/Deck.cs`:
```csharp
namespace CardCore;

public sealed class Deck
{
    private readonly List<Card> _cards;

    public Deck(IReadOnlyList<Card> cards, Random rng)
    {
        if (cards is null) throw new ArgumentNullException(nameof(cards));
        if (rng is null) throw new ArgumentNullException(nameof(rng));
        _cards = new List<Card>(cards);
        Shuffle(_cards, rng);
    }

    // Internal ctor used by replay: skips the shuffle and accepts a known order.
    internal Deck(IReadOnlyList<Card> shuffledOrder)
    {
        if (shuffledOrder is null) throw new ArgumentNullException(nameof(shuffledOrder));
        _cards = new List<Card>(shuffledOrder);
    }

    public int Count => _cards.Count;

    public Card this[int index] => _cards[index];

    public DeckRemoveResult RemoveTop()
    {
        if (_cards.Count == 0)
            throw new InvalidOperationException("Cannot remove from an empty deck.");
        var card = _cards[0];
        _cards.RemoveAt(0);
        return new DeckRemoveResult(card, IndexBefore: 0);
    }

    public Card FindCardById(int id)
    {
        foreach (var c in _cards)
            if (c.Id == id) return c;
        throw new InvalidOperationException($"No card with id {id} in deck.");
    }

    public IReadOnlyList<Card> Snapshot() => _cards.AsReadOnly();

    private static void Shuffle(List<Card> list, Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}

public readonly record struct DeckRemoveResult(Card Card, int IndexBefore);
```

- [ ] **Step 4: GREEN — verify pass**

Run: `dotnet test --filter FullyQualifiedName~DeckTests`
Expected: `Passed: 7, Failed: 0`.

- [ ] **Step 5: Commit**

```bash
git add Tests/PureCSharp/DeckTests.cs Runtime/Models/Deck.cs
git commit -m "Add Deck model with shuffle, RemoveTop, FindCardById"
```

---

## Task 7: `GameEvent` abstract base + concrete events

**Files:**
- Create: `Tests/PureCSharp/GameEventTests.cs`
- Create: `Runtime/GameEvent.cs`
- Create: `Runtime/Events/GameStarted.cs`
- Create: `Runtime/Events/CardDrawn.cs`
- Create: `Runtime/Events/CardPlayed.cs`

- [ ] **Step 1: RED — write tests for round-trip JSON polymorphism**

Create `Tests/PureCSharp/GameEventTests.cs`:
```csharp
using System.Text.Json;
using CardCore;
using CardCore.Events;
using Xunit;

namespace CardCore.PureTests;

public class GameEventTests
{
    [Fact]
    public void GameStarted_RoundTripsThroughJson()
    {
        var deck = new List<Card> { new(1, "A"), new(2, "B") };
        var evt = new GameStarted
        {
            SequenceId = 0,
            Timestamp = 1000,
            InitialDeckOrder = deck,
            PlayerCount = 2,
            Seed = 42,
        };

        var json = JsonSerializer.Serialize<GameEvent>(evt);
        var roundTrip = JsonSerializer.Deserialize<GameEvent>(json);

        var typed = Assert.IsType<GameStarted>(roundTrip);
        Assert.Equal(0, typed.SequenceId);
        Assert.Equal(1000, typed.Timestamp);
        Assert.Equal(2, typed.PlayerCount);
        Assert.Equal(42, typed.Seed);
        Assert.Equal(2, typed.InitialDeckOrder.Count);
        Assert.Equal(1, typed.InitialDeckOrder[0].Id);
    }

    [Fact]
    public void CardDrawn_RoundTripsThroughJson()
    {
        var evt = new CardDrawn
        {
            SequenceId = 1, Timestamp = 1001,
            PlayerId = 0, CardId = 7, DeckIndexBefore = 3,
        };
        var json = JsonSerializer.Serialize<GameEvent>(evt);
        var rt = Assert.IsType<CardDrawn>(JsonSerializer.Deserialize<GameEvent>(json));
        Assert.Equal(0, rt.PlayerId);
        Assert.Equal(7, rt.CardId);
        Assert.Equal(3, rt.DeckIndexBefore);
    }

    [Fact]
    public void CardPlayed_RoundTripsThroughJson()
    {
        var evt = new CardPlayed
        {
            SequenceId = 2, Timestamp = 1002,
            PlayerId = 1, CardId = 9,
            HandIndexBefore = 0, PlayAreaIndexAfter = 0,
        };
        var json = JsonSerializer.Serialize<GameEvent>(evt);
        var rt = Assert.IsType<CardPlayed>(JsonSerializer.Deserialize<GameEvent>(json));
        Assert.Equal(1, rt.PlayerId);
        Assert.Equal(9, rt.CardId);
        Assert.Equal(0, rt.HandIndexBefore);
        Assert.Equal(0, rt.PlayAreaIndexAfter);
    }

    [Fact]
    public void DiscriminatorIsSimpleTypeName()
    {
        var evt = new CardDrawn
        {
            SequenceId = 0, Timestamp = 0,
            PlayerId = 0, CardId = 0, DeckIndexBefore = 0,
        };
        var json = JsonSerializer.Serialize<GameEvent>(evt);
        Assert.Contains("\"$type\":\"CardDrawn\"", json);
    }
}
```

- [ ] **Step 2: RED — verify failure**

Run: `dotnet test --filter FullyQualifiedName~GameEventTests`
Expected: build errors — `GameEvent`, `GameStarted`, `CardDrawn`, `CardPlayed` do not exist.

- [ ] **Step 3: GREEN — create `GameEvent` base**

Create `Runtime/GameEvent.cs`:
```csharp
using System.Text.Json.Serialization;
using CardCore.Events;

namespace CardCore;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(GameStarted), "GameStarted")]
[JsonDerivedType(typeof(CardDrawn), "CardDrawn")]
[JsonDerivedType(typeof(CardPlayed), "CardPlayed")]
public abstract record GameEvent
{
    public int SequenceId { get; init; }
    public long Timestamp { get; init; }
}
```

- [ ] **Step 4: GREEN — create `GameStarted`**

Create `Runtime/Events/GameStarted.cs`:
```csharp
namespace CardCore.Events;

public sealed record GameStarted : GameEvent
{
    public IReadOnlyList<Card> InitialDeckOrder { get; init; } = Array.Empty<Card>();
    public int PlayerCount { get; init; }
    public int Seed { get; init; }
}
```

- [ ] **Step 5: GREEN — create `CardDrawn`**

Create `Runtime/Events/CardDrawn.cs`:
```csharp
namespace CardCore.Events;

public sealed record CardDrawn : GameEvent
{
    public int PlayerId { get; init; }
    public int CardId { get; init; }
    public int DeckIndexBefore { get; init; }
}
```

- [ ] **Step 6: GREEN — create `CardPlayed`**

Create `Runtime/Events/CardPlayed.cs`:
```csharp
namespace CardCore.Events;

public sealed record CardPlayed : GameEvent
{
    public int PlayerId { get; init; }
    public int CardId { get; init; }
    public int HandIndexBefore { get; init; }
    public int PlayAreaIndexAfter { get; init; }
}
```

- [ ] **Step 7: GREEN — verify pass**

Run: `dotnet test --filter FullyQualifiedName~GameEventTests`
Expected: `Passed: 4, Failed: 0`.

- [ ] **Step 8: Commit**

```bash
git add Tests/PureCSharp/GameEventTests.cs Runtime/GameEvent.cs Runtime/Events/
git commit -m "Add polymorphic GameEvent hierarchy"
```

---

## Task 8: `GameState` skeleton + `Apply(GameStarted)`

`GameState` is mutable internally (`Apply` is `internal`, only the engine calls it). For this task we wire up `GameStarted` only. Subsequent tasks add `CardDrawn` and `CardPlayed`.

**Files:**
- Create: `Tests/PureCSharp/GameStateTests.cs`
- Create: `Runtime/GameState.cs`

- [ ] **Step 1: RED — test that fresh state is "not started" with empty collections**

Create `Tests/PureCSharp/GameStateTests.cs`:
```csharp
using CardCore;
using CardCore.Events;
using Xunit;

namespace CardCore.PureTests;

public class GameStateTests
{
    [Fact]
    public void NewState_IsNotStarted_AndEmpty()
    {
        var s = new GameState();
        Assert.False(s.IsStarted);
        Assert.Empty(s.Players);
        Assert.Empty(s.PlayArea);
        Assert.Null(s.Deck);
    }

    [Fact]
    public void Apply_GameStarted_SeedsState()
    {
        var s = new GameState();
        var deck = new List<Card> { new(1, "A"), new(2, "B"), new(3, "C") };
        var evt = new GameStarted
        {
            SequenceId = 0, Timestamp = 0,
            InitialDeckOrder = deck, PlayerCount = 2, Seed = 42,
        };

        s.ApplyForTest(evt);

        Assert.True(s.IsStarted);
        Assert.Equal(2, s.Players.Count);
        Assert.Equal(0, s.Players[0].Id);
        Assert.Equal(1, s.Players[1].Id);
        Assert.Empty(s.PlayArea);
        Assert.NotNull(s.Deck);
        Assert.Equal(3, s.Deck!.Count);
        Assert.Equal(42, s.Seed);
    }

    [Fact]
    public void Apply_GameStartedTwice_Throws()
    {
        var s = new GameState();
        var deck = new List<Card> { new(1, "A") };
        var evt = new GameStarted
        {
            SequenceId = 0, Timestamp = 0,
            InitialDeckOrder = deck, PlayerCount = 1, Seed = 0,
        };
        s.ApplyForTest(evt);

        Assert.Throws<InvalidOperationException>(() => s.ApplyForTest(evt with { SequenceId = 1 }));
    }
}
```

The test calls `ApplyForTest`, an `internal` shim we'll expose; production `Apply` stays `internal` and only the engine calls it directly. `[InternalsVisibleTo("CardCore.PureTests")]` is already in the csproj.

- [ ] **Step 2: RED — verify failure**

Run: `dotnet test --filter FullyQualifiedName~GameStateTests`
Expected: build errors — `GameState` does not exist.

- [ ] **Step 3: GREEN — write `GameState` with `Apply(GameStarted)` only**

Create `Runtime/GameState.cs`:
```csharp
using CardCore.Events;

namespace CardCore;

public sealed class GameState
{
    private readonly List<Player> _players = new();
    private readonly List<Card> _playArea = new();
    private Deck? _deck;
    private int _seed;
    private bool _isStarted;

    public IReadOnlyList<Player> Players => _players;
    public IReadOnlyList<Card> PlayArea => _playArea;
    public Deck? Deck => _deck;
    public int Seed => _seed;
    public bool IsStarted => _isStarted;

    internal void Apply(GameEvent evt)
    {
        switch (evt)
        {
            case GameStarted started:
                ApplyGameStarted(started);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unknown event {evt.GetType().Name} at SequenceId {evt.SequenceId}.");
        }
    }

    // Test-only shim; same body as internal Apply. Exists so test code can
    // exercise Apply without going through the engine.
    internal void ApplyForTest(GameEvent evt) => Apply(evt);

    private void ApplyGameStarted(GameStarted evt)
    {
        if (_isStarted)
            throw new InvalidOperationException(
                $"GameStarted already applied; rejected at SequenceId {evt.SequenceId}.");
        _seed = evt.Seed;
        for (int i = 0; i < evt.PlayerCount; i++)
            _players.Add(new Player(i));
        _deck = new Deck(evt.InitialDeckOrder);
        _isStarted = true;
    }
}
```

- [ ] **Step 4: GREEN — verify pass**

Run: `dotnet test --filter FullyQualifiedName~GameStateTests`
Expected: `Passed: 3, Failed: 0`.

- [ ] **Step 5: Commit**

```bash
git add Tests/PureCSharp/GameStateTests.cs Runtime/GameState.cs
git commit -m "Add GameState with GameStarted apply"
```

---

## Task 9: Extend `GameState.Apply` for `CardDrawn` and `CardPlayed`

**Files:**
- Modify: `Tests/PureCSharp/GameStateTests.cs`
- Modify: `Runtime/GameState.cs`

- [ ] **Step 1: RED — add tests for CardDrawn**

Append to `GameStateTests` class:
```csharp
    private static GameState NewStartedState(int playerCount, params Card[] deck)
    {
        var s = new GameState();
        s.ApplyForTest(new GameStarted
        {
            SequenceId = 0, Timestamp = 0,
            InitialDeckOrder = deck, PlayerCount = playerCount, Seed = 0,
        });
        return s;
    }

    [Fact]
    public void Apply_CardDrawn_MovesTopOfDeckToHand()
    {
        var s = NewStartedState(1, new Card(1, "A"), new Card(2, "B"));

        s.ApplyForTest(new CardDrawn
        {
            SequenceId = 1, Timestamp = 0,
            PlayerId = 0, CardId = 1, DeckIndexBefore = 0,
        });

        Assert.Equal(1, s.Players[0].Hand.Count);
        Assert.Equal(1, s.Players[0].Hand[0].Id);
        Assert.Equal(1, s.Deck!.Count);
        Assert.Equal(2, s.Deck[0].Id);
    }

    [Fact]
    public void Apply_CardDrawn_OnEmptyDeck_Throws()
    {
        var s = NewStartedState(1);
        Assert.Throws<InvalidOperationException>(() => s.ApplyForTest(new CardDrawn
        {
            SequenceId = 1, Timestamp = 0,
            PlayerId = 0, CardId = 1, DeckIndexBefore = 0,
        }));
    }
```

- [ ] **Step 2: RED — verify failure**

Run: `dotnet test --filter FullyQualifiedName~GameStateTests`
Expected: 1 fail (`Apply_CardDrawn_MovesTopOfDeckToHand`) — switch falls through to `default` and throws.

- [ ] **Step 3: GREEN — extend Apply with CardDrawn**

In `Runtime/GameState.cs`, add the `case CardDrawn drawn` branch and helper method:

Replace the `switch` block in `Apply`:
```csharp
    internal void Apply(GameEvent evt)
    {
        switch (evt)
        {
            case GameStarted started:
                ApplyGameStarted(started);
                break;
            case CardDrawn drawn:
                ApplyCardDrawn(drawn);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unknown event {evt.GetType().Name} at SequenceId {evt.SequenceId}.");
        }
    }
```

Add the helper at the bottom of the class:
```csharp
    private void ApplyCardDrawn(CardDrawn evt)
    {
        if (_deck is null || _deck.Count == 0)
            throw new InvalidOperationException(
                $"CardDrawn against empty deck at SequenceId {evt.SequenceId}.");
        var top = _deck.RemoveTop();
        if (top.Card.Id != evt.CardId)
            throw new InvalidOperationException(
                $"CardDrawn.CardId mismatch at SequenceId {evt.SequenceId}.");
        _players[evt.PlayerId].Hand.Add(top.Card);
    }
```

- [ ] **Step 4: GREEN — verify pass**

Run: `dotnet test --filter FullyQualifiedName~GameStateTests`
Expected: all green.

- [ ] **Step 5: RED — add tests for CardPlayed**

Append to `GameStateTests` class:
```csharp
    [Fact]
    public void Apply_CardPlayed_MovesCardFromHandToPlayArea()
    {
        var s = NewStartedState(1, new Card(1, "A"), new Card(2, "B"));
        s.ApplyForTest(new CardDrawn
        {
            SequenceId = 1, Timestamp = 0,
            PlayerId = 0, CardId = 1, DeckIndexBefore = 0,
        });

        s.ApplyForTest(new CardPlayed
        {
            SequenceId = 2, Timestamp = 0,
            PlayerId = 0, CardId = 1, HandIndexBefore = 0, PlayAreaIndexAfter = 0,
        });

        Assert.Equal(0, s.Players[0].Hand.Count);
        Assert.Equal(1, s.PlayArea.Count);
        Assert.Equal(1, s.PlayArea[0].Id);
    }

    [Fact]
    public void Apply_CardPlayed_HandIndexOutOfRange_Throws()
    {
        var s = NewStartedState(1, new Card(1, "A"));
        Assert.Throws<InvalidOperationException>(() => s.ApplyForTest(new CardPlayed
        {
            SequenceId = 1, Timestamp = 0,
            PlayerId = 0, CardId = 1, HandIndexBefore = 0, PlayAreaIndexAfter = 0,
        }));
    }
```

- [ ] **Step 6: RED — verify failure**

Run: `dotnet test --filter FullyQualifiedName~GameStateTests`
Expected: 1 fail (`Apply_CardPlayed_MovesCardFromHandToPlayArea`).

- [ ] **Step 7: GREEN — extend Apply with CardPlayed**

In `Runtime/GameState.cs`, add `case CardPlayed played` to the switch:
```csharp
            case CardPlayed played:
                ApplyCardPlayed(played);
                break;
```

Add the helper at the bottom of the class:
```csharp
    private void ApplyCardPlayed(CardPlayed evt)
    {
        var hand = _players[evt.PlayerId].Hand;
        if (evt.HandIndexBefore < 0 || evt.HandIndexBefore >= hand.Count)
            throw new InvalidOperationException(
                $"CardPlayed.HandIndexBefore out of range at SequenceId {evt.SequenceId}.");
        var card = hand.RemoveAt(evt.HandIndexBefore);
        if (card.Id != evt.CardId)
            throw new InvalidOperationException(
                $"CardPlayed.CardId mismatch at SequenceId {evt.SequenceId}.");
        _playArea.Add(card);
    }
```

- [ ] **Step 8: GREEN — verify pass**

Run: `dotnet test --filter FullyQualifiedName~GameStateTests`
Expected: all green.

- [ ] **Step 9: Commit**

```bash
git add Tests/PureCSharp/GameStateTests.cs Runtime/GameState.cs
git commit -m "Extend GameState.Apply for CardDrawn and CardPlayed"
```

---

## Task 10: `IGameCommand` interface

**Files:**
- Create: `Runtime/Commands/IGameCommand.cs`

No test for the interface itself; tests come with the concrete commands in following tasks.

- [ ] **Step 1: Write the interface**

Create `Runtime/Commands/IGameCommand.cs`:
```csharp
namespace CardCore.Commands;

public interface IGameCommand
{
    bool CanExecute(GameState state);
    IReadOnlyList<GameEvent> Execute(GameState state);
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build`
Expected: success.

- [ ] **Step 3: Commit**

```bash
git add Runtime/Commands/IGameCommand.cs
git commit -m "Add IGameCommand interface"
```

---

## Task 11: `StartGameCommand`

**Files:**
- Create: `Tests/PureCSharp/StartGameCommandTests.cs`
- Create: `Runtime/Commands/StartGameCommand.cs`

The command captures the post-shuffle deck order in the emitted event, so replay never re-runs `System.Random`.

- [ ] **Step 1: RED — write tests**

Create `Tests/PureCSharp/StartGameCommandTests.cs`:
```csharp
using CardCore;
using CardCore.Commands;
using CardCore.Events;
using Xunit;

namespace CardCore.PureTests;

public class StartGameCommandTests
{
    private static List<Card> ThreeCards() => new()
    {
        new Card(1, "A"), new Card(2, "B"), new Card(3, "C"),
    };

    [Fact]
    public void Constructor_NullDeck_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new StartGameCommand(null!, playerCount: 2, seed: 0));
    }

    [Fact]
    public void Constructor_EmptyDeck_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new StartGameCommand(new List<Card>(), playerCount: 2, seed: 0));
    }

    [Fact]
    public void Constructor_DuplicateCardIds_Throws()
    {
        var dup = new List<Card> { new(1, "A"), new(1, "B") };
        Assert.Throws<ArgumentException>(() =>
            new StartGameCommand(dup, playerCount: 2, seed: 0));
    }

    [Fact]
    public void Constructor_ZeroPlayers_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new StartGameCommand(ThreeCards(), playerCount: 0, seed: 0));
    }

    [Fact]
    public void CanExecute_OnEmptyState_True()
    {
        var cmd = new StartGameCommand(ThreeCards(), 2, 0);
        Assert.True(cmd.CanExecute(new GameState()));
    }

    [Fact]
    public void CanExecute_OnStartedState_False()
    {
        var s = new GameState();
        s.ApplyForTest(new GameStarted
        {
            SequenceId = 0, Timestamp = 0,
            InitialDeckOrder = ThreeCards(), PlayerCount = 1, Seed = 0,
        });

        var cmd = new StartGameCommand(ThreeCards(), 2, 0);
        Assert.False(cmd.CanExecute(s));
    }

    [Fact]
    public void Execute_EmitsSingleGameStartedEvent_WithShuffledOrder()
    {
        var cmd = new StartGameCommand(ThreeCards(), playerCount: 2, seed: 42);

        var events = cmd.Execute(new GameState());

        Assert.Single(events);
        var started = Assert.IsType<GameStarted>(events[0]);
        Assert.Equal(2, started.PlayerCount);
        Assert.Equal(42, started.Seed);
        Assert.Equal(3, started.InitialDeckOrder.Count);
        // The order is whatever Random(42).Shuffle produces; the contract is
        // that the event captures it explicitly.
    }

    [Fact]
    public void Execute_SameSeed_ProducesSameOrder()
    {
        var cmd1 = new StartGameCommand(ThreeCards(), 2, 42);
        var cmd2 = new StartGameCommand(ThreeCards(), 2, 42);

        var s1 = (GameStarted)cmd1.Execute(new GameState())[0];
        var s2 = (GameStarted)cmd2.Execute(new GameState())[0];

        Assert.Equal(s1.InitialDeckOrder.Select(c => c.Id),
                     s2.InitialDeckOrder.Select(c => c.Id));
    }
}
```

- [ ] **Step 2: RED — verify failure**

Run: `dotnet test --filter FullyQualifiedName~StartGameCommandTests`
Expected: build errors — `StartGameCommand` does not exist.

- [ ] **Step 3: GREEN — write `StartGameCommand`**

Create `Runtime/Commands/StartGameCommand.cs`:
```csharp
using CardCore.Events;

namespace CardCore.Commands;

public sealed class StartGameCommand : IGameCommand
{
    private readonly IReadOnlyList<Card> _deck;
    private readonly int _playerCount;
    private readonly int _seed;

    public StartGameCommand(IReadOnlyList<Card> deck, int playerCount, int seed)
    {
        if (deck is null) throw new ArgumentNullException(nameof(deck));
        if (deck.Count == 0)
            throw new ArgumentException("Deck must be non-empty.", nameof(deck));
        if (playerCount < 1)
            throw new ArgumentException("Player count must be >= 1.", nameof(playerCount));
        var ids = new HashSet<int>();
        foreach (var c in deck)
            if (!ids.Add(c.Id))
                throw new ArgumentException(
                    $"Duplicate card id {c.Id} in deck.", nameof(deck));

        _deck = deck;
        _playerCount = playerCount;
        _seed = seed;
    }

    public bool CanExecute(GameState state) => !state.IsStarted;

    public IReadOnlyList<GameEvent> Execute(GameState state)
    {
        var rng = new Random(_seed);
        var shuffled = new List<Card>(_deck);
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        return new GameEvent[]
        {
            new GameStarted
            {
                InitialDeckOrder = shuffled,
                PlayerCount = _playerCount,
                Seed = _seed,
            }
        };
    }
}
```

- [ ] **Step 4: GREEN — verify pass**

Run: `dotnet test --filter FullyQualifiedName~StartGameCommandTests`
Expected: `Passed: 8, Failed: 0`.

- [ ] **Step 5: Commit**

```bash
git add Tests/PureCSharp/StartGameCommandTests.cs Runtime/Commands/StartGameCommand.cs
git commit -m "Add StartGameCommand"
```

---

## Task 12: `DrawCardCommand`

**Files:**
- Create: `Tests/PureCSharp/DrawCardCommandTests.cs`
- Create: `Runtime/Commands/DrawCardCommand.cs`

- [ ] **Step 1: RED — write tests**

Create `Tests/PureCSharp/DrawCardCommandTests.cs`:
```csharp
using CardCore;
using CardCore.Commands;
using CardCore.Events;
using Xunit;

namespace CardCore.PureTests;

public class DrawCardCommandTests
{
    private static GameState StartedState(int playerCount, params Card[] deck)
    {
        var s = new GameState();
        s.ApplyForTest(new GameStarted
        {
            SequenceId = 0, Timestamp = 0,
            InitialDeckOrder = deck, PlayerCount = playerCount, Seed = 0,
        });
        return s;
    }

    [Fact]
    public void Constructor_NegativePlayerId_Throws()
    {
        Assert.Throws<ArgumentException>(() => new DrawCardCommand(-1));
    }

    [Fact]
    public void CanExecute_GameNotStarted_False()
    {
        Assert.False(new DrawCardCommand(0).CanExecute(new GameState()));
    }

    [Fact]
    public void CanExecute_EmptyDeck_False()
    {
        var s = StartedState(1);
        Assert.False(new DrawCardCommand(0).CanExecute(s));
    }

    [Fact]
    public void CanExecute_InvalidPlayerId_False()
    {
        var s = StartedState(1, new Card(1, "A"));
        Assert.False(new DrawCardCommand(5).CanExecute(s));
    }

    [Fact]
    public void CanExecute_Valid_True()
    {
        var s = StartedState(1, new Card(1, "A"));
        Assert.True(new DrawCardCommand(0).CanExecute(s));
    }

    [Fact]
    public void Execute_EmitsSingleCardDrawnEvent_FromTopOfDeck()
    {
        var s = StartedState(1, new Card(7, "Top"), new Card(8, "Next"));
        var cmd = new DrawCardCommand(0);

        var events = cmd.Execute(s);

        Assert.Single(events);
        var drawn = Assert.IsType<CardDrawn>(events[0]);
        Assert.Equal(0, drawn.PlayerId);
        Assert.Equal(7, drawn.CardId);
        Assert.Equal(0, drawn.DeckIndexBefore);
    }
}
```

- [ ] **Step 2: RED — verify failure**

Run: `dotnet test --filter FullyQualifiedName~DrawCardCommandTests`
Expected: build errors.

- [ ] **Step 3: GREEN — write `DrawCardCommand`**

Create `Runtime/Commands/DrawCardCommand.cs`:
```csharp
using CardCore.Events;

namespace CardCore.Commands;

public sealed class DrawCardCommand : IGameCommand
{
    private readonly int _playerId;

    public DrawCardCommand(int playerId)
    {
        if (playerId < 0)
            throw new ArgumentException("playerId must be >= 0.", nameof(playerId));
        _playerId = playerId;
    }

    public bool CanExecute(GameState state)
    {
        if (!state.IsStarted) return false;
        if (state.Deck is null || state.Deck.Count == 0) return false;
        if (_playerId < 0 || _playerId >= state.Players.Count) return false;
        return true;
    }

    public IReadOnlyList<GameEvent> Execute(GameState state)
    {
        var top = state.Deck![0];
        return new GameEvent[]
        {
            new CardDrawn
            {
                PlayerId = _playerId,
                CardId = top.Id,
                DeckIndexBefore = 0,
            }
        };
    }
}
```

- [ ] **Step 4: GREEN — verify pass**

Run: `dotnet test --filter FullyQualifiedName~DrawCardCommandTests`
Expected: `Passed: 6, Failed: 0`.

- [ ] **Step 5: Commit**

```bash
git add Tests/PureCSharp/DrawCardCommandTests.cs Runtime/Commands/DrawCardCommand.cs
git commit -m "Add DrawCardCommand"
```

---

## Task 13: `PlayCardCommand`

**Files:**
- Create: `Tests/PureCSharp/PlayCardCommandTests.cs`
- Create: `Runtime/Commands/PlayCardCommand.cs`

- [ ] **Step 1: RED — write tests**

Create `Tests/PureCSharp/PlayCardCommandTests.cs`:
```csharp
using CardCore;
using CardCore.Commands;
using CardCore.Events;
using Xunit;

namespace CardCore.PureTests;

public class PlayCardCommandTests
{
    private static GameState StartedWithCardInHand()
    {
        var s = new GameState();
        s.ApplyForTest(new GameStarted
        {
            SequenceId = 0, Timestamp = 0,
            InitialDeckOrder = new List<Card> { new(7, "X") },
            PlayerCount = 1, Seed = 0,
        });
        s.ApplyForTest(new CardDrawn
        {
            SequenceId = 1, Timestamp = 0,
            PlayerId = 0, CardId = 7, DeckIndexBefore = 0,
        });
        return s;
    }

    [Fact]
    public void Constructor_NegativePlayerId_Throws()
    {
        Assert.Throws<ArgumentException>(() => new PlayCardCommand(-1, 0));
    }

    [Fact]
    public void Constructor_NegativeHandIndex_Throws()
    {
        Assert.Throws<ArgumentException>(() => new PlayCardCommand(0, -1));
    }

    [Fact]
    public void CanExecute_GameNotStarted_False()
    {
        Assert.False(new PlayCardCommand(0, 0).CanExecute(new GameState()));
    }

    [Fact]
    public void CanExecute_HandIndexOutOfRange_False()
    {
        var s = StartedWithCardInHand();
        Assert.False(new PlayCardCommand(0, 5).CanExecute(s));
    }

    [Fact]
    public void CanExecute_Valid_True()
    {
        var s = StartedWithCardInHand();
        Assert.True(new PlayCardCommand(0, 0).CanExecute(s));
    }

    [Fact]
    public void Execute_EmitsSingleCardPlayedEvent()
    {
        var s = StartedWithCardInHand();
        var cmd = new PlayCardCommand(0, 0);

        var events = cmd.Execute(s);

        Assert.Single(events);
        var played = Assert.IsType<CardPlayed>(events[0]);
        Assert.Equal(0, played.PlayerId);
        Assert.Equal(7, played.CardId);
        Assert.Equal(0, played.HandIndexBefore);
        Assert.Equal(0, played.PlayAreaIndexAfter);
    }
}
```

- [ ] **Step 2: RED — verify failure**

Run: `dotnet test --filter FullyQualifiedName~PlayCardCommandTests`
Expected: build errors.

- [ ] **Step 3: GREEN — write `PlayCardCommand`**

Create `Runtime/Commands/PlayCardCommand.cs`:
```csharp
using CardCore.Events;

namespace CardCore.Commands;

public sealed class PlayCardCommand : IGameCommand
{
    private readonly int _playerId;
    private readonly int _handIndex;

    public PlayCardCommand(int playerId, int handIndex)
    {
        if (playerId < 0)
            throw new ArgumentException("playerId must be >= 0.", nameof(playerId));
        if (handIndex < 0)
            throw new ArgumentException("handIndex must be >= 0.", nameof(handIndex));
        _playerId = playerId;
        _handIndex = handIndex;
    }

    public bool CanExecute(GameState state)
    {
        if (!state.IsStarted) return false;
        if (_playerId < 0 || _playerId >= state.Players.Count) return false;
        var hand = state.Players[_playerId].Hand;
        return _handIndex >= 0 && _handIndex < hand.Count;
    }

    public IReadOnlyList<GameEvent> Execute(GameState state)
    {
        var card = state.Players[_playerId].Hand[_handIndex];
        return new GameEvent[]
        {
            new CardPlayed
            {
                PlayerId = _playerId,
                CardId = card.Id,
                HandIndexBefore = _handIndex,
                PlayAreaIndexAfter = state.PlayArea.Count,
            }
        };
    }
}
```

- [ ] **Step 4: GREEN — verify pass**

Run: `dotnet test --filter FullyQualifiedName~PlayCardCommandTests`
Expected: `Passed: 6, Failed: 0`.

- [ ] **Step 5: Commit**

```bash
git add Tests/PureCSharp/PlayCardCommandTests.cs Runtime/Commands/PlayCardCommand.cs
git commit -m "Add PlayCardCommand"
```

---

## Task 14: `IGameEngine` interface + `GameEngine` skeleton

The engine wires the pieces together. This task implements `ExecuteCommand`, `GetEventLog`, `GetCurrentState` (without the clone yet — clone comes in Task 15).

**Files:**
- Create: `Tests/PureCSharp/GameEngineTests.cs`
- Create: `Runtime/IGameEngine.cs`
- Create: `Runtime/GameEngine.cs`

- [ ] **Step 1: RED — write integration tests for the basic loop**

Create `Tests/PureCSharp/GameEngineTests.cs`:
```csharp
using CardCore;
using CardCore.Commands;
using CardCore.Events;
using Xunit;

namespace CardCore.PureTests;

public class GameEngineTests
{
    private static List<Card> SmallDeck() => new()
    {
        new Card(1, "A"), new Card(2, "B"), new Card(3, "C"),
    };

    [Fact]
    public void NewEngine_HasEmptyLog()
    {
        var engine = new GameEngine();
        Assert.Empty(engine.GetEventLog());
    }

    [Fact]
    public void ExecuteCommand_StartGame_AppendsAndApplies()
    {
        var engine = new GameEngine();
        var cmd = new StartGameCommand(SmallDeck(), 2, 42);

        var emitted = engine.ExecuteCommand(cmd);

        Assert.Single(emitted);
        Assert.Equal(1, engine.GetEventLog().Count);
        Assert.True(engine.GetCurrentState().IsStarted);
    }

    [Fact]
    public void ExecuteCommand_AssignsSequenceIdsContiguouslyFromZero()
    {
        var engine = new GameEngine();
        engine.ExecuteCommand(new StartGameCommand(SmallDeck(), 1, 0));
        engine.ExecuteCommand(new DrawCardCommand(0));

        var log = engine.GetEventLog();
        Assert.Equal(2, log.Count);
        Assert.Equal(0, log[0].SequenceId);
        Assert.Equal(1, log[1].SequenceId);
    }

    [Fact]
    public void ExecuteCommand_AssignsTimestampsMonotonically()
    {
        var engine = new GameEngine();
        engine.ExecuteCommand(new StartGameCommand(SmallDeck(), 1, 0));
        engine.ExecuteCommand(new DrawCardCommand(0));

        var log = engine.GetEventLog();
        Assert.True(log[1].Timestamp >= log[0].Timestamp);
    }

    [Fact]
    public void ExecuteCommand_FailsCanExecute_Throws()
    {
        var engine = new GameEngine();
        // DrawCardCommand against an unstarted state.
        Assert.Throws<InvalidOperationException>(
            () => engine.ExecuteCommand(new DrawCardCommand(0)));
    }

    [Fact]
    public void ThreeCommandSequence_ProducesExpectedState()
    {
        var engine = new GameEngine();
        engine.ExecuteCommand(new StartGameCommand(SmallDeck(), 1, 42));
        engine.ExecuteCommand(new DrawCardCommand(0));
        engine.ExecuteCommand(new PlayCardCommand(0, 0));

        var state = engine.GetCurrentState();
        Assert.Equal(0, state.Players[0].Hand.Count);
        Assert.Equal(1, state.PlayArea.Count);
        Assert.Equal(2, state.Deck!.Count);
    }
}
```

- [ ] **Step 2: RED — verify failure**

Run: `dotnet test --filter FullyQualifiedName~GameEngineTests`
Expected: build errors.

- [ ] **Step 3: GREEN — write `IGameEngine`**

Create `Runtime/IGameEngine.cs`:
```csharp
using CardCore.Commands;

namespace CardCore;

public interface IGameEngine
{
    IReadOnlyList<GameEvent> ExecuteCommand(IGameCommand command);
    IReadOnlyList<GameEvent> GetEventLog();
    GameState GetStateAtIndex(int eventIndex);
    GameState GetCurrentState();
    void LoadEventLog(IReadOnlyList<GameEvent> events);
}
```

- [ ] **Step 4: GREEN — write `GameEngine` (no clone yet — Task 15 adds it)**

Create `Runtime/GameEngine.cs`:
```csharp
using CardCore.Commands;

namespace CardCore;

public sealed class GameEngine : IGameEngine
{
    private readonly List<GameEvent> _log = new();
    private readonly GameState _state = new();

    public IReadOnlyList<GameEvent> GetEventLog() => _log.AsReadOnly();

    public IReadOnlyList<GameEvent> ExecuteCommand(IGameCommand command)
    {
        if (command is null) throw new ArgumentNullException(nameof(command));
        if (!command.CanExecute(_state))
            throw new InvalidOperationException(
                $"Command {command.GetType().Name} failed CanExecute against current state.");

        var raw = command.Execute(_state);
        var stamped = new List<GameEvent>(raw.Count);
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        foreach (var evt in raw)
        {
            var withMeta = evt with { SequenceId = _log.Count, Timestamp = now };
            _log.Add(withMeta);
            _state.Apply(withMeta);
            stamped.Add(withMeta);
        }

        return stamped.AsReadOnly();
    }

    public GameState GetCurrentState() => _state; // clone added in Task 15

    public GameState GetStateAtIndex(int eventIndex)
    {
        throw new NotImplementedException("Added in Task 15.");
    }

    public void LoadEventLog(IReadOnlyList<GameEvent> events)
    {
        throw new NotImplementedException("Added in Task 16.");
    }
}
```

Note: `evt with { ... }` uses C# `record` non-destructive mutation — works because `GameEvent` is a `record`. The polymorphic subclass type is preserved.

- [ ] **Step 5: GREEN — verify pass**

Run: `dotnet test --filter FullyQualifiedName~GameEngineTests`
Expected: `Passed: 6, Failed: 0`.

- [ ] **Step 6: Commit**

```bash
git add Tests/PureCSharp/GameEngineTests.cs Runtime/IGameEngine.cs Runtime/GameEngine.cs
git commit -m "Add GameEngine with ExecuteCommand and event-log basics"
```

---

## Task 15: `GameEngine.GetStateAtIndex` + clone-on-exit

Adds replay-from-zero (`GetStateAtIndex`) and the JSON-roundtrip clone for both `GetCurrentState` and `GetStateAtIndex`.

**Files:**
- Modify: `Tests/PureCSharp/GameEngineTests.cs`
- Modify: `Runtime/GameEngine.cs`

- [ ] **Step 1: RED — add tests**

Append to `GameEngineTests` class:
```csharp
    [Fact]
    public void GetStateAtIndex_ZeroAfterStartGame_ReflectsStart()
    {
        var engine = new GameEngine();
        engine.ExecuteCommand(new StartGameCommand(SmallDeck(), 1, 0));

        var s = engine.GetStateAtIndex(0);
        Assert.True(s.IsStarted);
        Assert.Equal(3, s.Deck!.Count);
        Assert.Equal(0, s.Players[0].Hand.Count);
    }

    [Fact]
    public void GetStateAtIndex_AfterMultipleEvents_ReflectsExactPoint()
    {
        var engine = new GameEngine();
        engine.ExecuteCommand(new StartGameCommand(SmallDeck(), 1, 0));
        engine.ExecuteCommand(new DrawCardCommand(0)); // index 1
        engine.ExecuteCommand(new PlayCardCommand(0, 0)); // index 2

        var sAt1 = engine.GetStateAtIndex(1);
        Assert.Equal(1, sAt1.Players[0].Hand.Count);
        Assert.Equal(0, sAt1.PlayArea.Count);

        var sAt2 = engine.GetStateAtIndex(2);
        Assert.Equal(0, sAt2.Players[0].Hand.Count);
        Assert.Equal(1, sAt2.PlayArea.Count);
    }

    [Fact]
    public void GetStateAtIndex_OutOfRange_Throws()
    {
        var engine = new GameEngine();
        engine.ExecuteCommand(new StartGameCommand(SmallDeck(), 1, 0));

        Assert.Throws<ArgumentOutOfRangeException>(() => engine.GetStateAtIndex(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.GetStateAtIndex(99));
    }

    [Fact]
    public void GetCurrentState_ReturnsClone_NotLiveReference()
    {
        var engine = new GameEngine();
        engine.ExecuteCommand(new StartGameCommand(SmallDeck(), 1, 0));
        engine.ExecuteCommand(new DrawCardCommand(0));

        var s1 = engine.GetCurrentState();
        var s2 = engine.GetCurrentState();

        // Two clones, distinct instances.
        Assert.NotSame(s1, s2);
        // Mutating one (via internal hook) does not affect the other.
        s1.ApplyForTest(new CardPlayed
        {
            SequenceId = 99, Timestamp = 0,
            PlayerId = 0, CardId = s1.Players[0].Hand[0].Id,
            HandIndexBefore = 0, PlayAreaIndexAfter = 0,
        });
        Assert.Equal(1, s2.Players[0].Hand.Count);
    }
```

- [ ] **Step 2: RED — verify failure**

Run: `dotnet test --filter FullyQualifiedName~GameEngineTests`
Expected: 4 fails (NotImplementedException + same-instance returns).

- [ ] **Step 3: GREEN — replace stubs in `GameEngine`**

In `Runtime/GameEngine.cs`, replace `GetCurrentState` and `GetStateAtIndex`:
```csharp
    public GameState GetCurrentState() => _state.Clone();

    public GameState GetStateAtIndex(int eventIndex)
    {
        if (eventIndex < 0 || eventIndex >= _log.Count)
            throw new ArgumentOutOfRangeException(nameof(eventIndex));
        var s = new GameState();
        for (int i = 0; i <= eventIndex; i++)
            s.Apply(_log[i]);
        return s.Clone();
    }
```

- [ ] **Step 4: GREEN — add `Clone()` to `GameState`**

In `Runtime/GameState.cs`, add at the bottom of the class:
```csharp
    internal GameState Clone()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(this, CloneOptions);
        return System.Text.Json.JsonSerializer.Deserialize<GameState>(json, CloneOptions)!;
    }

    private static readonly System.Text.Json.JsonSerializerOptions CloneOptions = new()
    {
        IncludeFields = false,
    };
```

`GameState` needs to round-trip: the public properties expose the data, but the *fields* are private and there's no parameterless deserialization path. Add a JSON-deserialization-friendly constructor:

Replace the top of `GameState` (right after the field declarations) with:
```csharp
    [System.Text.Json.Serialization.JsonConstructor]
    internal GameState(
        IReadOnlyList<Player>? players = null,
        IReadOnlyList<Card>? playArea = null,
        Deck? deck = null,
        int seed = 0,
        bool isStarted = false)
    {
        if (players is not null) _players.AddRange(players);
        if (playArea is not null) _playArea.AddRange(playArea);
        _deck = deck;
        _seed = seed;
        _isStarted = isStarted;
    }

    public GameState() : this(null, null, null, 0, false) { }
```

Note: this means `Player`, `Hand`, and `Deck` also need JSON-friendly deserialization paths. Continue with the next steps to add them.

- [ ] **Step 5: GREEN — make `Player` JSON-roundtrippable**

Replace `Runtime/Models/Player.cs`:
```csharp
namespace CardCore;

public sealed class Player
{
    public int Id { get; }
    public Hand Hand { get; }

    public Player(int id) : this(id, new Hand()) { }

    [System.Text.Json.Serialization.JsonConstructor]
    internal Player(int id, Hand hand)
    {
        if (id < 0) throw new ArgumentException("Player.Id must be >= 0.", nameof(id));
        Id = id;
        Hand = hand ?? new Hand();
    }
}
```

- [ ] **Step 6: GREEN — make `Hand` JSON-roundtrippable**

Replace `Runtime/Models/Hand.cs`:
```csharp
using System.Text.Json.Serialization;

namespace CardCore;

public sealed class Hand
{
    private readonly List<Card> _cards;

    public Hand() : this(null) { }

    [JsonConstructor]
    internal Hand(IReadOnlyList<Card>? cards)
    {
        _cards = cards is null ? new List<Card>() : new List<Card>(cards);
    }

    public int Count => _cards.Count;

    public IReadOnlyList<Card> Cards => _cards.AsReadOnly();

    public Card this[int index] => _cards[index];

    public void Add(Card card)
    {
        if (card is null) throw new ArgumentNullException(nameof(card));
        _cards.Add(card);
    }

    public Card RemoveAt(int index)
    {
        if (index < 0 || index >= _cards.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        var card = _cards[index];
        _cards.RemoveAt(index);
        return card;
    }
}
```

- [ ] **Step 7: GREEN — make `Deck` JSON-roundtrippable**

In `Runtime/Models/Deck.cs`, replace the internal constructor and add a JSON-friendly one. Replace the constructors block (everything before `public int Count`) with:

```csharp
    public Deck(IReadOnlyList<Card> cards, Random rng)
    {
        if (cards is null) throw new ArgumentNullException(nameof(cards));
        if (rng is null) throw new ArgumentNullException(nameof(rng));
        _cards = new List<Card>(cards);
        Shuffle(_cards, rng);
    }

    // Used by replay (state.ApplyGameStarted): skips the shuffle.
    internal Deck(IReadOnlyList<Card> shuffledOrder)
    {
        if (shuffledOrder is null) throw new ArgumentNullException(nameof(shuffledOrder));
        _cards = new List<Card>(shuffledOrder);
    }

    // Used by JSON deserialization (clone path).
    [System.Text.Json.Serialization.JsonConstructor]
    internal Deck(IReadOnlyList<Card> cards)
    {
        if (cards is null) throw new ArgumentNullException(nameof(cards));
        _cards = new List<Card>(cards);
    }
```

Wait — that produces two `internal Deck(IReadOnlyList<Card>)` overloads with the same signature. Replace the entire constructors block instead with a single ambiguity-free version:

```csharp
    public Deck(IReadOnlyList<Card> cards, Random rng)
    {
        if (cards is null) throw new ArgumentNullException(nameof(cards));
        if (rng is null) throw new ArgumentNullException(nameof(rng));
        _cards = new List<Card>(cards);
        Shuffle(_cards, rng);
    }

    // Used by replay AND JSON deserialization (no shuffle, accepts known order).
    [System.Text.Json.Serialization.JsonConstructor]
    internal Deck(IReadOnlyList<Card> cards)
    {
        if (cards is null) throw new ArgumentNullException(nameof(cards));
        _cards = new List<Card>(cards);
    }
```

Also add a property for JSON to read from. Replace the `public int Count =>` line with:
```csharp
    public int Count => _cards.Count;
    public IReadOnlyList<Card> Cards => _cards.AsReadOnly();
```

And update `state.ApplyGameStarted` in `Runtime/GameState.cs` to use the new internal ctor (its signature is unchanged: `new Deck(IReadOnlyList<Card>)`). No code change needed there — the call already matches.

- [ ] **Step 8: GREEN — verify all tests pass**

Run: `dotnet test`
Expected: all green.

- [ ] **Step 9: Commit**

```bash
git add Runtime/ Tests/PureCSharp/GameEngineTests.cs
git commit -m "Add GetStateAtIndex and clone-on-exit (JSON round-trip)"
```

---

## Task 16: `GameEngine.LoadEventLog` + the replay invariant

The headline test of the whole spec.

**Files:**
- Modify: `Tests/PureCSharp/GameEngineTests.cs`
- Modify: `Runtime/GameEngine.cs`

- [ ] **Step 1: RED — write the replay-invariant test plus load-validation tests**

Append to `GameEngineTests` class:
```csharp
    [Fact]
    public void EventReplay_ReconstructsIdenticalState()
    {
        var engineA = new GameEngine();
        engineA.ExecuteCommand(new StartGameCommand(SmallDeck(), 1, 42));
        engineA.ExecuteCommand(new DrawCardCommand(0));
        engineA.ExecuteCommand(new PlayCardCommand(0, 0));
        engineA.ExecuteCommand(new DrawCardCommand(0));

        var json = System.Text.Json.JsonSerializer.Serialize(engineA.GetEventLog());
        var loaded = System.Text.Json.JsonSerializer.Deserialize<List<GameEvent>>(json)!;

        var engineB = new GameEngine();
        engineB.LoadEventLog(loaded);

        var jsonA = System.Text.Json.JsonSerializer.Serialize(engineA.GetCurrentState());
        var jsonB = System.Text.Json.JsonSerializer.Serialize(engineB.GetCurrentState());
        Assert.Equal(jsonA, jsonB);
    }

    [Fact]
    public void LoadEventLog_NullEvents_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new GameEngine().LoadEventLog(null!));
    }

    [Fact]
    public void LoadEventLog_FirstEventNotGameStarted_Throws()
    {
        var bogus = new List<GameEvent>
        {
            new CardDrawn { SequenceId = 0, Timestamp = 0, PlayerId = 0, CardId = 1, DeckIndexBefore = 0 },
        };
        Assert.Throws<InvalidOperationException>(() => new GameEngine().LoadEventLog(bogus));
    }

    [Fact]
    public void LoadEventLog_NonContiguousSequenceIds_Throws()
    {
        var deck = SmallDeck();
        var bogus = new List<GameEvent>
        {
            new GameStarted { SequenceId = 0, Timestamp = 0, InitialDeckOrder = deck, PlayerCount = 1, Seed = 0 },
            new CardDrawn { SequenceId = 5, Timestamp = 0, PlayerId = 0, CardId = deck[0].Id, DeckIndexBefore = 0 },
        };
        Assert.Throws<InvalidOperationException>(() => new GameEngine().LoadEventLog(bogus));
    }

    [Fact]
    public void LoadEventLog_DuplicateGameStarted_Throws()
    {
        var deck = SmallDeck();
        var bogus = new List<GameEvent>
        {
            new GameStarted { SequenceId = 0, Timestamp = 0, InitialDeckOrder = deck, PlayerCount = 1, Seed = 0 },
            new GameStarted { SequenceId = 1, Timestamp = 0, InitialDeckOrder = deck, PlayerCount = 1, Seed = 0 },
        };
        Assert.Throws<InvalidOperationException>(() => new GameEngine().LoadEventLog(bogus));
    }

    [Fact]
    public void LoadEventLog_OnEngineWithExistingLog_Throws()
    {
        var engine = new GameEngine();
        engine.ExecuteCommand(new StartGameCommand(SmallDeck(), 1, 0));

        var deck = SmallDeck();
        var more = new List<GameEvent>
        {
            new GameStarted { SequenceId = 0, Timestamp = 0, InitialDeckOrder = deck, PlayerCount = 1, Seed = 0 },
        };
        Assert.Throws<InvalidOperationException>(() => engine.LoadEventLog(more));
    }
```

- [ ] **Step 2: RED — verify failure**

Run: `dotnet test --filter FullyQualifiedName~GameEngineTests.EventReplay_ReconstructsIdenticalState`
Expected: throws `NotImplementedException` from `LoadEventLog`.

- [ ] **Step 3: GREEN — implement `LoadEventLog`**

In `Runtime/GameEngine.cs`, replace `LoadEventLog`:
```csharp
    public void LoadEventLog(IReadOnlyList<GameEvent> events)
    {
        if (events is null) throw new ArgumentNullException(nameof(events));
        if (_log.Count > 0)
            throw new InvalidOperationException("Engine already has events; LoadEventLog requires a fresh engine.");
        if (events.Count == 0) return;

        // Validate before applying anything, so failure leaves engine pristine.
        if (events[0] is not Events.GameStarted)
            throw new InvalidOperationException("First event must be GameStarted.");
        for (int i = 0; i < events.Count; i++)
        {
            if (events[i].SequenceId != i)
                throw new InvalidOperationException(
                    $"Non-contiguous SequenceId at position {i}: expected {i}, got {events[i].SequenceId}.");
            if (i > 0 && events[i] is Events.GameStarted)
                throw new InvalidOperationException(
                    $"Duplicate GameStarted at SequenceId {events[i].SequenceId}.");
        }

        foreach (var evt in events)
        {
            _log.Add(evt);
            _state.Apply(evt);
        }
    }
```

- [ ] **Step 4: GREEN — verify all GameEngine tests pass**

Run: `dotnet test --filter FullyQualifiedName~GameEngineTests`
Expected: all green, including the replay invariant.

- [ ] **Step 5: GREEN — verify the entire suite passes**

Run: `dotnet test`
Expected: all green across every test file.

- [ ] **Step 6: Commit**

```bash
git add Runtime/GameEngine.cs Tests/PureCSharp/GameEngineTests.cs
git commit -m "Add LoadEventLog with validation and replay invariant test"
```

---

## Task 17: README and final polish

**Files:**
- Create: `README.md`

- [ ] **Step 1: Write `README.md`**

Create `README.md`:
```markdown
# CardCore

Headless event-sourced card game engine. Pure C#, no Unity dependencies.

## Use

Add as a Unity Package via Git URL:

```
"com.crosshatch.cardcore": "https://github.com/<owner>/cardcore.git"
```

Or reference `Runtime/CardCore.csproj` directly from any .NET 8+ project.

## Quickstart

```csharp
using CardCore;
using CardCore.Commands;

var deck = new[] { new Card(1, "Copper"), new Card(2, "Silver"), new Card(3, "Gold") };
var engine = new GameEngine();

engine.ExecuteCommand(new StartGameCommand(deck, playerCount: 2, seed: 42));
engine.ExecuteCommand(new DrawCardCommand(playerId: 0));
engine.ExecuteCommand(new PlayCardCommand(playerId: 0, handIndex: 0));

var state = engine.GetCurrentState();
Console.WriteLine($"PlayArea: {state.PlayArea.Count} card(s)");
```

## Architecture

See `docs/superpowers/specs/2026-04-27-cardcore-walking-skeleton-design.md`.

## Tests

```
dotnet test
```
```

- [ ] **Step 2: Verify suite**

Run: `dotnet test`
Expected: all green.

- [ ] **Step 3: Commit**

```bash
git add README.md
git commit -m "Add README"
```

---

## Done

At the end of Task 17, the walking skeleton is complete:

- 3 commands (`StartGame`, `DrawCard`, `PlayCard`) round-trip through 3 events (`GameStarted`, `CardDrawn`, `CardPlayed`).
- `GameEngine` appends events, applies them, replays from any index, validates and loads external logs.
- The replay invariant (`engineA.state == engineB.state` after `LoadEventLog`) is verified.
- Every public class is reached by xUnit tests; the test suite is the spec.
- `Tests/Runtime/` placeholder is in place for future Unity-side tests.
- Standards: all fields private, types sealed, validate-in-constructor, no Unity references.

Future slices (out of scope here): real card format with effects, board / `IGamePiece` / `IBoard`, turn management and scoring, deck-builder migration, simulation runner, snapshots.
