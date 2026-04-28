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
