

# \#\#Mission

I am a solo game developer with decades  of experience in game design. We are making a tool that allows me to rapidly prototype board games. These boardgames are digital first. If successful, I will look into printing them. 

It needs to be generic enough that I can make several boardgames with it. It also needs to be modular with strong encapsulation so that I can reuse as much as possible. We will narrow the scope by setting boundaries on the design space. 

I have to understand how it works. So I will ask certain software design patterns to be used because I am more familiar with them. When a pattern doesn’t fit, or you have a better option, I expect you to suggest it. 

Card Core is meant to be a headless service. I want to use it across several unity projects and may even branch out to other engines like Godot. 

# \#\#Your Role (Claude CLI)

You are responsible for creating, testing, and maintaining the Card Core service. You will maintain and enforce standards for the building of custom games on top of cardcore. You are a service provider for client code and projects.   
You also need to maintain up to date diagrammatic representations of the code base. This should be updated every time you update the project code. 

You live in a github repository. Other clients will import your latest package

# \#\# Rules (non-negotiable)

- Card Core must be runnable in a headless (no UI) mode  
- All fields MUST be \`private\` (or \`private readonly\` when possible). No \`public\` or \`internal\` fields ever — expose state via properties.   
- Prefer \`init\`-only or \`private set\` properties; use full \`set\` only when external mutation is genuinely required.   
- Use \`readonly record struct\` / \`record\` for value-like data. \- Validate in setters or constructors — never let an invalid object exist.  
-  Mutating operations live as methods on the owning class, not as external code reaching into properties.  
-  Internals: prefer \`internal\` over \`public\` unless a type is part of the public API surface. Use \`\[InternalsVisibleTo\]\` for test access. \- Constructors should fully initialize the object; no two-phase init.  
- Default to \`sealed\` classes unless inheritance is part of the design.  
- \- No \`using UnityEngine\`  
- \- No \`MonoBehaviour\`  
- \- All classes must be serializable to JSON  
- \- Use \`System.Random\` not \`UnityEngine.Random\`  
- \- All public methods return JSON-serializable types  
- 

# \#\# Preferences

  Unity Package via Git URL (recommended for your use case)  
    \- Move Assets/Scripts/CardCore/ to a separate repo (or a Packages/ subfolder structure)  
    \- Add package.json manifest  
    \- Other Unity projects add it via Package Manager: https://github.com/you/cardcore.git  
    \- Source-level inclusion, immutable to consumers, version-pinnable, debuggable  
    \- Updates by changing the git ref

# \#\#Architecture

CardCore is a pure C\# card game engine using \*\*Event Sourcing architecture\*\*. The engine is completely decoupled from Unity and communicates with UI layers through immutable event streams.  
\*\*Core Principle\*\*: Commands create Events. Events are the source of truth. State is derived by replaying events.

\`\`\`  
┌─────────────────────────────────────────────┐  
│         CardCore (Pure C\#)                   │  
│         No Unity dependencies                │  
├─────────────────────────────────────────────┤  
│  IGameEngine                                │  
│  ├── ExecuteCommand(cmd) → GameEvent\[\]      │  
│  ├── GetEventLog() → List\<GameEvent\>        │  
│  ├── GetStateAtIndex(idx) → GameState      │  
│  └── LoadEventLog(events)                   │  
│                                             │  
│  GameEvent (JSON serializable)              │  
│  ├── SequenceId (int)                       │  
│  ├── EventType (string)                     │  
│  ├── DataJson (string)                      │  
│  └── Timestamp (long)                       │  
│                                             │  
│  IGameCommand                               │  
│  └── Execute(state) → GameEvent\[\]          │  
└─────────────────────────────────────────────┘  
              │  
              │ JSON Event Stream  
              ▼  
┌─────────────────────────────────────────────┐  
│      UI Layer (Unity MonoBehaviour)          │  
├─────────────────────────────────────────────┤  
│  GameEventPlayer                            │  
│  ├── CurrentEventIndex (scrubber position)  │  
│  ├── EventLog (List\<GameEvent\>)            │  
│  ├── StepForward() / StepBackward()         │  
│  └── JumpToIndex(int)                       │  
│                                             │  
│  IEventVisualizer (per event type)          │  
│  └── VisualizeAsync(GameEvent) → Task      │  
│                                             │  
│  Event-specific Visualizers                 │  
│  ├── CardDrawnVisualizer                    │  
│  ├── CardPlayedVisualizer                   │  
│  └── All run independently (async)          │  
└─────────────────────────────────────────────┘

\*\*UI Layer (Assets/Scripts/UI/)\*\*: Unity-specific  
\- MonoBehaviour components  
\- Consumes CardCore via interface only  
\- Never modifies CardCore state directly  
\- Reads events, visualizes asynchronously  
\- UI layer is implemented in 3D space with cards that are prefabs.  
\- Don't use legacy UI  
\- Don't use prebuild UI  
\- Don’t use legacy input

\#\# Core Interfaces  
\#\#\# IGameEngine (CardCore)  
\`\`\`csharp  
namespace CardCore  
{  
    public interface IGameEngine  
    {  
        /// \<summary\>  
        /// Execute a command and append resulting events to the log  
        /// \</summary\>  
        List\<GameEvent\> ExecuteCommand(IGameCommand command);  
        /// \<summary\>  
        /// Get complete event log (source of truth)  
        /// \</summary\>  
        List\<GameEvent\> GetEventLog();  
        /// \<summary\>  
        /// Rebuild game state by replaying events 0 to index  
        /// \</summary\>  
        GameState GetStateAtIndex(int eventIndex);  
        /// \<summary\>  
        /// Load a previously saved event log (for replay/network sync)  
        /// \</summary\>  
        void LoadEventLog(List\<GameEvent\> events);  
        /// \<summary\>  
        /// Get current state (equivalent to GetStateAtIndex(log.Count \- 1))  
        /// \</summary\>  
        GameState GetCurrentState();  
    }  
}  
\`\`\`  
\#\#\# GameEvent (CardCore)  
\`\`\`csharp  
using Newtonsoft.Json;  
namespace CardCore  
{  
    \[Serializable\]  
    public class GameEvent  
    {  
        public int SequenceId { get; set; }  
        public string EventType { get; set; } // "CardDrawn", "CardPlayed", etc.  
        public string DataJson { get; set; }  // Serialized event-specific data  
        public long Timestamp { get; set; }   // Unix timestamp  
        public T DeserializeData\<T\>() where T : class  
        {  
            return JsonConvert.DeserializeObject\<T\>(DataJson);  
        }  
        public static GameEvent Create\<T\>(string eventType, T data) where T : class  
        {  
            return new GameEvent  
            {  
                EventType \= eventType,  
                DataJson \= JsonConvert.SerializeObject(data),  
                Timestamp \= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()  
            };  
        }  
    }  
}  
\`\`\`  
\#\#\# IGameCommand (CardCore)  
\`\`\`csharp  
namespace CardCore  
{  
    public interface IGameCommand  
    {  
        /// \<summary\>  
        /// Execute command against current state, return events generated  
        /// Commands should be pure functions (no side effects)  
        /// \</summary\>  
        List\<GameEvent\> Execute(GameState currentState);  
        /// \<summary\>  
        /// Validate if command can be executed against state  
        /// \</summary\>  
        bool CanExecute(GameState currentState);  
    }  
}  
\`\`\`  
\#\#\# Example Command Implementation  
\`\`\`csharp  
namespace CardCore.Commands  
{  
    public class PlayCardCommand : IGameCommand  
    {  
        public int PlayerId { get; set; }  
        public int CardIndex { get; set; }  
        public List\<GameEvent\> Execute(GameState state)  
        {  
            var events \= new List\<GameEvent\>();  
            var player \= state.Players\[PlayerId\];  
            var card \= player.Hand\[CardIndex\];  
            // Event 1: Card removed from hand  
            events.Add(GameEvent.Create("CardRemovedFromHand", new CardRemovedData  
            {  
                PlayerId \= PlayerId,  
                CardId \= card.Id,  
                HandIndex \= CardIndex  
            }));  
            // Event 2: Card added to play area  
            events.Add(GameEvent.Create("CardAddedToPlayArea", new CardAddedData  
            {  
                CardId \= card.Id,  
                Position \= state.PlayArea.Count  
            }));  
            // Event 3+: Trigger any card effects (if applicable)  
            var effectEvents \= card.TriggerEffects(state);  
            events.AddRange(effectEvents);  
            return events;  
        }  
        public bool CanExecute(GameState state)  
        {  
            return state.CurrentPlayerId \== PlayerId  
                && CardIndex \>= 0  
                && CardIndex \< state.Players\[PlayerId\].Hand.Count;  
        }  
    }  
}  
\`\`\`  
\#\#\# GameState (CardCore)  
\`\`\`csharp  
using Newtonsoft.Json;  
namespace CardCore  
{  
    \[Serializable\]  
    public class GameState  
    {  
        public List\<Player\> Players { get; set; }  
        public List\<Card\> PlayArea { get; set; }  
        public Deck Deck { get; set; }  
        public int CurrentPlayerId { get; set; }  
        public string GamePhase { get; set; } // "Setup", "Playing", "GameOver"  
        /// \<summary\>  
        /// Apply a single event to this state (mutates state)  
        /// \</summary\>  
        public void ApplyEvent(GameEvent evt)  
        {  
            switch (evt.EventType)  
            {  
                case "CardDrawn":  
                    var drawData \= evt.DeserializeData\<CardDrawnData\>();  
                    var card \= Deck.RemoveAt(drawData.DeckIndex);  
                    Players\[drawData.PlayerId\].Hand.Add(card);  
                    break;  
                case "CardRemovedFromHand":  
                    var removeData \= evt.DeserializeData\<CardRemovedData\>();  
                    Players\[removeData.PlayerId\].Hand.RemoveAt(removeData.HandIndex);  
                    break;  
                case "CardAddedToPlayArea":  
                    var addData \= evt.DeserializeData\<CardAddedData\>();  
                    PlayArea.Add(Deck.FindCardById(addData.CardId));  
                    break;  
                // ... handle all event types  
            }  
        }  
        /// \<summary\>  
        /// Deep copy this state  
        /// \</summary\>  
        public GameState Clone()  
        {  
            var json \= JsonConvert.SerializeObject(this);  
            return JsonConvert.DeserializeObject\<GameState\>(json);  
        }  
    }  
}  
\`\`\`  
\#\# UI Layer Implementation  
\#\#\# GameEventPlayer (Unity MonoBehaviour)  
\`\`\`csharp  
using UnityEngine;  
using CardCore;  
public class GameEventPlayer : MonoBehaviour  
{  
    private IGameEngine \_engine;  
    private List\<GameEvent\> \_eventLog;  
    private int \_currentEventIndex \= \-1;  
    private Dictionary\<string, IEventVisualizer\> \_visualizers;  
    public int CurrentEventIndex \=\> \_currentEventIndex;  
    public int TotalEvents \=\> \_eventLog?.Count ?? 0;  
    public void Initialize(IGameEngine engine)  
    {  
        \_engine \= engine;  
        \_eventLog \= \_engine.GetEventLog();  
        RegisterVisualizers();  
    }  
    /// \<summary\>  
    /// Move scrubber forward one event  
    /// \</summary\>  
    public async void StepForward()  
    {  
        if (\_currentEventIndex \>= \_eventLog.Count \- 1\) return;  
        \_currentEventIndex++;  
        var evt \= \_eventLog\[\_currentEventIndex\];  
        await VisualizeEvent(evt);  
    }  
    /// \<summary\>  
    /// Move scrubber backward one event  
    /// \</summary\>  
    public async void StepBackward()  
    {  
        if (\_currentEventIndex \< 0\) return;  
        var evt \= \_eventLog\[\_currentEventIndex\];  
        await ReverseVisualizeEvent(evt);  
        \_currentEventIndex--;  
    }  
    /// \<summary\>  
    /// Jump to arbitrary point in event log  
    /// \</summary\>  
    public async void JumpToIndex(int targetIndex)  
    {  
        if (targetIndex \< 0 || targetIndex \>= \_eventLog.Count) return;  
        // Rebuild state at target index  
        var state \= \_engine.GetStateAtIndex(targetIndex);  
        // Refresh entire UI from state  
        RefreshUIFromState(state);  
        \_currentEventIndex \= targetIndex;  
    }  
    /// \<summary\>  
    /// Play events from current position to end at specified speed  
    /// \</summary\>  
    public async Task PlayToEnd(float eventsPerSecond \= 5f)  
    {  
        while (\_currentEventIndex \< \_eventLog.Count \- 1\)  
        {  
            StepForward();  
            await Task.Delay((int)(1000f / eventsPerSecond));  
        }  
    }  
    private async Task VisualizeEvent(GameEvent evt)  
    {  
        if (\_visualizers.TryGetValue(evt.EventType, out var visualizer))  
        {  
            await visualizer.VisualizeAsync(evt);  
        }  
    }  
    private async Task ReverseVisualizeEvent(GameEvent evt)  
    {  
        if (\_visualizers.TryGetValue(evt.EventType, out var visualizer))  
        {  
            await visualizer.ReverseVisualizeAsync(evt);  
        }  
    }  
    private void RegisterVisualizers()  
    {  
        \_visualizers \= new Dictionary\<string, IEventVisualizer\>  
        {  
            { "CardDrawn", GetComponent\<CardDrawnVisualizer\>() },  
            { "CardPlayed", GetComponent\<CardPlayedVisualizer\>() },  
            // ... register all event types  
        };  
    }  
    private void RefreshUIFromState(GameState state)  
    {  
        // Snapshot refresh \- set all UI to match state  
        GetComponent\<HandView\>().DisplayCards(state.Players\[0\].Hand);  
        GetComponent\<PlayAreaView\>().DisplayCards(state.PlayArea);  
        GetComponent\<DeckView\>().SetCardCount(state.Deck.CardCount);  
    }  
}  
\`\`\`  
\#\#\# IEventVisualizer (UI Layer)  
\`\`\`csharp  
public interface IEventVisualizer  
{  
    /// \<summary\>  
    /// Visualize event playing forward (async for animations)  
    /// \</summary\>  
    Task VisualizeAsync(GameEvent evt);  
    /// \<summary\>  
    /// Visualize event playing backward (reverse animation)  
    /// \</summary\>  
    Task ReverseVisualizeAsync(GameEvent evt);  
}  
\`\`\`  
\#\#\# Example Visualizer  
\`\`\`csharp  
using UnityEngine;  
using CardCore;  
public class CardDrawnVisualizer : MonoBehaviour, IEventVisualizer  
{  
    public async Task VisualizeAsync(GameEvent evt)  
    {  
        var data \= evt.DeserializeData\<CardDrawnData\>();  
        // Create card visual  
        var cardObj \= CardPool.Instance.GetCard();  
        var cardView \= cardObj.GetComponent\<CardView\>();  
        // Position at deck  
        cardView.transform.position \= DeckPosition;  
        // Animate to player hand  
        await cardView.AnimateToHand(data.PlayerId, data.HandIndex);  
    }  
    public async Task ReverseVisualizeAsync(GameEvent evt)  
    {  
        var data \= evt.DeserializeData\<CardDrawnData\>();  
        // Find card in hand  
        var cardView \= HandView.GetCardAtIndex(data.PlayerId, data.HandIndex);  
        // Animate back to deck  
        await cardView.AnimateToDeck();  
        // Return to pool  
        CardPool.Instance.ReturnCard(cardView.gameObject);  
    }  
}  
\`\`\`

\#\# Event Types Reference  
Common event types to implement:  
\- \`GameStarted\` \- Initial setup complete  
\- \`CardDrawn\` \- Card moved from deck to hand  
\- \`CardPlayed\` \- Card moved from hand to play area  
\- \`CardDiscarded\` \- Card moved to discard pile  
\- \`TurnStarted\` \- Player's turn begins  
\- \`TurnEnded\` \- Player's turn ends  
\- \`EffectTriggered\` \- Special card effect activated  
\- \`ScoreChanged\` \- Player score updated  
\- \`GameEnded\` \- Game over condition met  
Each event type needs:  
1\. Event data class (serializable)  
2\. State.ApplyEvent() case handler  
3\. UI Visualizer implementation  
\#\# Testing Strategy  
\#\#\# Unit Tests (Pure C\# \- No Unity)  
\`\`\`csharp  
\[Test\]  
public void PlayCardCommand\_GeneratesCorrectEvents()  
{  
    // Arrange  
    var state \= TestHelpers.CreateGameWithPlayerHolding(cardId: 42);  
    var command \= new PlayCardCommand { PlayerId \= 0, CardIndex \= 0 };  
    // Act  
    var events \= command.Execute(state);  
    // Assert  
    Assert.AreEqual(2, events.Count);  
    Assert.AreEqual("CardRemovedFromHand", events\[0\].EventType);  
    Assert.AreEqual("CardAddedToPlayArea", events\[1\].EventType);  
}  
\[Test\]  
public void EventReplay\_ReconstructsIdenticalState()  
{  
    // Arrange  
    var engine \= new GameEngine();  
    engine.ExecuteCommand(new StartGameCommand());  
    engine.ExecuteCommand(new DrawCardCommand { PlayerId \= 0 });  
    engine.ExecuteCommand(new PlayCardCommand { PlayerId \= 0, CardIndex \= 0 });  
    var eventLog \= engine.GetEventLog();  
    var finalState \= engine.GetCurrentState();  
    // Act \- Replay from scratch  
    var newEngine \= new GameEngine();  
    newEngine.LoadEventLog(eventLog);  
    var replayedState \= newEngine.GetCurrentState();  
    // Assert  
    Assert.AreEqual(  
        JsonConvert.SerializeObject(finalState),  
        JsonConvert.SerializeObject(replayedState)  
    );  
}  
\`\`\`  
\#\#\# Integration Tests (Unity Test Framework)  
\`\`\`csharp  
\[UnityTest\]  
public IEnumerator Scrubber\_CanStepForwardAndBackward()  
{  
    // Arrange  
    var eventPlayer \= CreateEventPlayer();  
    ExecuteTestGame(); // Creates event log  
    // Act \- Step forward 3 times  
    for (int i \= 0; i \< 3; i++)  
    {  
        eventPlayer.StepForward();  
        yield return new WaitForSeconds(0.5f);  
    }  
    int forwardIndex \= eventPlayer.CurrentEventIndex;  
    // Step backward 2 times  
    for (int i \= 0; i \< 2; i++)  
    {  
        eventPlayer.StepBackward();  
        yield return new WaitForSeconds(0.5f);  
    }  
    // Assert  
    Assert.AreEqual(forwardIndex \- 2, eventPlayer.CurrentEventIndex);  
}  
\`\`\`  
\#\# Networking  
Event sourcing is ideal for networking:  
Make sure this project is compatible with networking  
This project will not implement networking. A future version will  
\#\# Common Patterns  
\#\#\# Snapshot Optimization  
This project won't worry about optimization yet  
\#\#\# Complex Action Sequences  
Some game actions trigger cascading events (e.g., playing a card that causes other cards to be drawn):  
\`\`\`csharp  
public class PlayWildDrawFourCommand : IGameCommand  
{  
    public List\<GameEvent\> Execute(GameState state)  
    {  
        var events \= new List\<GameEvent\>();  
        // Event 1: Card played  
        events.Add(GameEvent.Create("CardPlayed", ...));  
        // Event 2: Next player draws 4 cards  
        int nextPlayer \= (state.CurrentPlayerId \+ 1\) % state.Players.Count;  
        for (int i \= 0; i \< 4; i++)  
        {  
            events.Add(GameEvent.Create("CardDrawn", new CardDrawnData  
            {  
                PlayerId \= nextPlayer,  
                DeckIndex \= 0 // Top of deck  
            }));  
        }  
        // Event 3: Turn skipped  
        events.Add(GameEvent.Create("TurnSkipped", ...));  
        return events;  
    }  
}  
\`\`\`

UI visualizers run these events asynchronously \- they appear simultaneous at high speeds but are logically sequential.  
\#\# File Organization  
\`\`\`  
Assets/  
├── Scripts/  
│   ├── CardCore/              \# PURE C\# \- No Unity  
│   │   ├── IGameEngine.cs  
│   │   ├── GameEngine.cs  
│   │   ├── GameEvent.cs  
│   │   ├── GameState.cs  
│   │   ├── Commands/  
│   │   │   ├── IGameCommand.cs  
│   │   │   ├── PlayCardCommand.cs  
│   │   │   └── DrawCardCommand.cs  
│   │   ├── Models/  
│   │   │   ├── Card.cs  
│   │   │   ├── Deck.cs  
│   │   │   ├── Hand.cs  
│   │   │   └── Player.cs  
│   │   └── EventData/  
│   │       ├── CardDrawnData.cs  
│   │       └── CardPlayedData.cs  
│   │  
│   └── UI/                    \# Unity MonoBehaviour  
│       ├── GameEventPlayer.cs  
│       ├── IEventVisualizer.cs  
│       ├── Visualizers/  
│       │   ├── CardDrawnVisualizer.cs  
│       │   └── CardPlayedVisualizer.cs  
│       └── Views/  
│           ├── HandView.cs  
│           ├── PlayAreaView.cs  
│           └── CardView.cs  
\`\`\`  
\#\# C\# Conventions  
\- \*\*Classes, Methods, Properties\*\*: \`PascalCase\`  
\- \*\*Private fields\*\*: \`\_camelCase\` with underscore  
\- \*\*Interfaces\*\*: \`IPascalCase\`  
\- \*\*Event types\*\*: String constants, PascalCase (e.g., "CardDrawn")  
\#\# Performance Notes  
\- Event log grows linearly with game length  
\- Use snapshots every N events for faster state reconstruction  
\- JSON serialization is the bottleneck \- consider MessagePack for production  
\- UI visualizers should be async but don't block scrubber navigation

\#\# Answers to Claude’s questions

1\. \*\*What game are we actually building first?\*\* The engine is generic — do you have a target game (your own design, a known one like Hearts/Crazy Eights, or a CCG like Hearthstone-lite) that should drive command/event design?  
Answer: We will build a deck building game of my own design. There is already a front end built. Next step of the project will be to convert it to use the card core back end

2\. \*\*Testing strategy — when do we start?\*\* Zero tests today. Do we want to write xUnit tests for \`GameEngine\`/\`ApplyEvent\` first, or keep iterating on the visual/Unity side and add tests later?  
Answer: Let’s talk about how to implement tests

4\. \*\*Scene \+ prefab work — who builds it?\*\* \`PROJECT\_STATUS.md\` lists card prefab, scene, and timeline UI as remaining Unity-editor work. Are you doing that by hand, or do you want guidance on the structure?  
Answer: We’ll adapt that legacy project to this architecture as a next step. First build the system, then help me test it. I have card data ready to go

5\. \*\*AI / simulation priority?\*\* The standalone plan includes \`IPlayerController\`, \`RandomPlayer\`, and a 100k-game simulation runner. Is balance/simulation testing actually a near-term goal, or aspirational?  
Answer: This is aspirational. Keep it in mind for architecture decisions. Core first, then testing, then Game UI, then simulation

6\. \*\*Serialization — Newtonsoft, System.Text.Json, or custom text format?\*\* The standalone design proposes adapter interfaces and a built-in \`ToEventString()\` pipe-delimited format. Which way do you want to go?  
Answer: Let’s talk about the options. We are starting fresh, so we can start over

7\. \*\*Snapshot optimization — when?\*\* \`CLAUDE.md\` says "won't worry about optimization yet," but \`GetStateAtIndex\` replays from event 0 every call. At what game length does that become a real problem for the scrubber?  
Answer: Give me some rough ballpark figures. When does the scrubber break? Is it a UI problem or a memory issue?

8\. \*\*Card identity model — IDs only, or richer?\*\* \`Card\` has an \`Id\`; events reference cards by ID and carry full Card objects in some places (e.g. \`CardPlayedData.Card\`). Is that duplication intentional for replay self-sufficiency, or something to clean up?  
Answer: Cards are much richer, I have a card format that I want to use. The goal is to make the game rules headless as well at some point. This will be a later step

9\. \*\*Multi-player / hidden information?\*\* Current \`GameState\` exposes everyone's hands. For games with hidden hands (most card games), do you want player-perspective state filtering, or handle it in the UI layer?  
Answer: Let’s handle it in the UI layer. I am not worried about hacking or cheating at this point

10\. \*\*Board game extension — real plan or future-maybe?\*\* The standalone doc mentions abstracting \`Card\` → \`GamePiece\`, adding \`Board\`/\`Cell\`/\`Movement\`. Is that a real roadmap item, or just keeping the door open?  
Answer: We’ll add the board functionality sooner than later. Keep this as stubs, we’ll flesh out soon

\- \[Project purpose\](project\_purpose.md) — CardCore is a prototyping toolkit for designing/testing card game prototypes that share mechanics \+ UX, not a single-game engine  
\- \[Packaging plan\](project\_packaging.md) — CardCore extracts to its own repo as Unity Package via Git URL; UI layer stays in-project until 3rd prototype reveals shared patterns  
\- \[Naming convention\](project\_naming.md) — \`com.crosshatch.cardcore\` reverse-DNS prefix; future: \`.unity\`, \`.simulations\` siblings  
\- \[DLL decision\](feedback\_no\_dll.md) — User rejected DLL approach in favor of Unity Package via Git URL; encapsulation comes from asmdef, not DLL  
\- \[Premature extraction guidance\](feedback\_premature\_extraction.md) — Extract engine now (clean API), defer UI layer extraction until 3rd prototype reveals shared patterns  
\- \[User profile\](user\_profile.md) — Solo game designer at Crosshatch Games; building prototyping infrastructure for own use  
\- \[Existing UI project\](project\_existing\_ui\_project.md) — Reminder: separate Unity project with hand-built UI exists; future Claude session will convert it to use CardCore

