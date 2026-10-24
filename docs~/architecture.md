# CardCore architecture

Last updated: 2026-10-24 (added `RulesetEvent` / `EmitRulesetEventCommand`).
Update this file in the same change as any code change under `Runtime/`.

## Data flow

```mermaid
flowchart LR
    Client["Client (Unity / Godot / sim)"] -->|IGameCommand| Engine["GameEngine (IGameEngine)"]
    Engine -->|CanExecute / Execute| Cmd["Commands"]
    Cmd -->|GameEvent list| Engine
    Engine -->|append| Log[("Event log")]
    Log -->|"Apply (internal)"| State["GameState"]
    Engine -->|"clone via JSON"| Client
    Ruleset["IRuleset + ActionDispatcher"] -->|"IActionHandler.Handle -> events"| Cmd
    Ruleset -.->|"replays RulesetEvent to derive own state"| Log
```

## Engine surface (`IGameEngine`)

```mermaid
classDiagram
    class IGameEngine {
        ExecuteCommand(IGameCommand)
        GetEventLog()
        GetStateAtIndex(int)
        GetCurrentState()
        LoadEventLog(events)
        GetDeckCount(playerId)
        GetDiscardCount(playerId)
        MutateLiveCardAction(instanceId, actionIndex, Action)
    }
    class GameEngine
    IGameEngine <|.. GameEngine
    GameEngine --> GameState : owns live state, hands out clones
```

## Commands and the events they emit

```mermaid
flowchart LR
    StartGameCommand --> GameStarted
    DrawCardCommand --> CardDrawn
    PlayCardCommand --> CardPlayed
    DiscardCommand --> CardDiscarded
    DestroyCardCommand --> CardDestroyed
    MoveDiscardToDeckCommand --> DiscardMovedToDeck
    ShuffleDeckCommand --> DeckShuffled
    EmitRulesetEventCommand --> RulesetEvent
```

`RulesetEvent` is the only event the engine does not interpret: `GameState.Apply` is a no-op for it.
Everything else mutates `GameState`.

## State model

```mermaid
classDiagram
    class GameState {
        Players
        PlayArea
        Deck
        Seed
        IsStarted
    }
    class Player { Id }
    class Hand
    class DiscardPile
    class Deck
    class CardInstance { InstanceId }
    class CardDefinition
    class Action { Verb, Payload }
    GameState --> Player
    GameState --> Deck
    GameState --> CardInstance : PlayArea
    Player --> Hand
    Player --> DiscardPile
    Hand --> CardInstance
    DiscardPile --> CardInstance
    Deck --> CardInstance
    CardInstance --> CardDefinition
    CardInstance --> Action : live action list
```

## Card data and ruleset

```mermaid
flowchart LR
    Source["Card source (JSON/markdown)"] --> Loader["CardCatalogLoader"]
    Loader --> Catalog["CardCatalog"]
    Catalog --> Def["CardDefinition"]
    Def --> Text["MarkdownText (parsed by MarkdownParser)"]
    Dispatcher["ActionDispatcher"] --> Handler["IActionHandler (per verb)"]
    Ruleset["IRuleset"] --> Dispatcher
```
