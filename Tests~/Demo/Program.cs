using System;
using System.Collections.Generic;
using System.IO;
using CardCore;
using CardCore.Catalog;
using CardCore.Commands;
using CardCore.Events;
using Newtonsoft.Json;

namespace CardCore.Demo;

internal static class Program
{
    private static int Main()
    {
        Section("1. Load catalog from JSON");
        var catalogPath = Path.Combine(AppContext.BaseDirectory, "catalog.json");
        var catalog = CardCatalogLoader.LoadFromJson(File.ReadAllText(catalogPath));
        Console.WriteLine($"Loaded {catalog.Count} card definition(s) from {Path.GetFileName(catalogPath)}");
        foreach (var def in catalog.Definitions)
        {
            Console.WriteLine($"  - {def.Id}: {def.Name.Raw}");
        }

        Section("2. Build a deck (host owns deck composition)");
        var deck = new List<CardInstance>
        {
            CardInstance.From(catalog.Get("copper")),
            CardInstance.From(catalog.Get("copper")),
            CardInstance.From(catalog.Get("silver")),
            CardInstance.From(catalog.Get("gold")),
            CardInstance.From(catalog.Get("estate")),
        };
        Console.WriteLine($"Deck size: {deck.Count}");
        foreach (var inst in deck)
        {
            Console.WriteLine($"  - {inst.DefinitionId}  (instance {inst.InstanceId})");
        }

        Section("3. Drive the engine: Start → Draw × 2 → Play");
        var engine = new GameEngine();
        Run(engine, new StartGameCommand(deck, playerCount: 1, seed: 42));
        Run(engine, new DrawCardCommand(0));
        Run(engine, new DrawCardCommand(0));
        Run(engine, new PlayCardCommand(playerId: 0, handIndex: 0));

        Section("4. Final state");
        var state = engine.GetCurrentState();
        Console.WriteLine($"  Players      : {state.Players.Count}");
        Console.WriteLine($"  Hand[0]      : {state.Players[0].Hand.Count} card(s)");
        foreach (var c in state.Players[0].Hand.Cards) Console.WriteLine($"      - {c.DefinitionId}");
        Console.WriteLine($"  PlayArea     : {state.PlayArea.Count} card(s)");
        foreach (var c in state.PlayArea) Console.WriteLine($"      - {c.DefinitionId}");
        Console.WriteLine($"  Deck remaining: {state.Deck!.Count}");

        Section("5. Event log → JSON → reload → identical state");
        var log = engine.GetEventLog();
        var json = JsonConvert.SerializeObject(log, GameEvent.JsonSettings);
        Console.WriteLine($"Serialized log: {json.Length} chars across {log.Count} events");

        var revived = JsonConvert.DeserializeObject<List<GameEvent>>(json, GameEvent.JsonSettings)!;
        var replay = new GameEngine();
        replay.LoadEventLog(revived);

        var stateA = JsonConvert.SerializeObject(engine.GetCurrentState(), GameEvent.JsonSettings);
        var stateB = JsonConvert.SerializeObject(replay.GetCurrentState(), GameEvent.JsonSettings);
        var match = stateA == stateB;
        Console.WriteLine($"Round-tripped state matches original: {match}");
        if (!match)
        {
            Console.WriteLine("--- ORIGINAL ---");
            Console.WriteLine(stateA);
            Console.WriteLine("--- REPLAYED ---");
            Console.WriteLine(stateB);
            return 1;
        }

        Section("6. Time-travel: state at index 1 (just after StartGame)");
        var early = engine.GetStateAtIndex(0);
        Console.WriteLine($"  Hand[0] cards: {early.Players[0].Hand.Count}");
        Console.WriteLine($"  Deck remaining: {early.Deck!.Count}");
        Console.WriteLine($"  PlayArea: {early.PlayArea.Count}");

        Console.WriteLine();
        Console.WriteLine("DEMO OK");
        return 0;
    }

    private static void Run(IGameEngine engine, IGameCommand command)
    {
        var events = engine.ExecuteCommand(command);
        Console.WriteLine($"  {command.GetType().Name} → {events.Count} event(s):");
        foreach (var e in events)
        {
            Console.WriteLine($"    [{e.SequenceId}] {e.GetType().Name}");
        }
    }

    private static void Section(string title)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 60));
        Console.WriteLine(title);
        Console.WriteLine(new string('=', 60));
    }
}
