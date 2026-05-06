using System;
using System.Collections.Generic;
using CardCore;
using Newtonsoft.Json.Linq;
using Xunit;
using Action = CardCore.Action;

namespace CardCore.PureTests;

public class ActionDispatcherTests
{
    private sealed class StubHandler : IActionHandler
    {
        public string Verb { get; }
        public Action? LastAction { get; private set; }
        public CardInstance? LastCard { get; private set; }
        public GameState? LastState { get; private set; }
        public IReadOnlyList<GameEvent> Result { get; }

        public StubHandler(string verb, IReadOnlyList<GameEvent>? result = null)
        {
            Verb = verb;
            Result = result ?? Array.Empty<GameEvent>();
        }

        public IReadOnlyList<GameEvent> Handle(Action action, CardInstance card, GameState state)
        {
            LastAction = action;
            LastCard = card;
            LastState = state;
            return Result;
        }
    }

    private static CardInstance NewInstance() =>
        CardInstance.From(new CardDefinition("c"));

    [Fact]
    public void Register_ThenIsRegistered_ReturnsTrue()
    {
        var dispatcher = new ActionDispatcher();
        dispatcher.Register(new StubHandler("draw"));

        Assert.True(dispatcher.IsRegistered("draw"));
        Assert.False(dispatcher.IsRegistered("discard"));
    }

    [Fact]
    public void Register_NullHandler_Throws()
    {
        var dispatcher = new ActionDispatcher();
        Assert.Throws<ArgumentNullException>(() => dispatcher.Register(null!));
    }

    [Fact]
    public void Register_DuplicateVerb_Throws()
    {
        var dispatcher = new ActionDispatcher();
        dispatcher.Register(new StubHandler("draw"));

        Assert.Throws<InvalidOperationException>(() =>
            dispatcher.Register(new StubHandler("draw")));
    }

    [Fact]
    public void Dispatch_RoutesToCorrectHandler_PassingArgs()
    {
        var dispatcher = new ActionDispatcher();
        var draw = new StubHandler("draw");
        var discard = new StubHandler("discard");
        dispatcher.Register(draw);
        dispatcher.Register(discard);

        var action = new Action("draw", new JObject());
        var card = NewInstance();
        var state = new GameState();

        var result = dispatcher.Dispatch(action, card, state);

        Assert.Empty(result);
        Assert.Same(action, draw.LastAction);
        Assert.Same(card, draw.LastCard);
        Assert.Same(state, draw.LastState);
        Assert.Null(discard.LastAction);
    }

    [Fact]
    public void Dispatch_UnknownVerb_Throws()
    {
        var dispatcher = new ActionDispatcher();
        Assert.Throws<InvalidOperationException>(() =>
            dispatcher.Dispatch(new Action("unknown", new JObject()), NewInstance(), new GameState()));
    }
}
