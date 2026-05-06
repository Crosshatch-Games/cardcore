using System;
using Newtonsoft.Json.Linq;
using Xunit;
using Action = CardCore.Action;

namespace CardCore.PureTests;

public class ActionTests
{
    [Fact]
    public void Constructor_WithValidVerbAndPayload_SetsProperties()
    {
        var payload = JObject.Parse("{\"target\":\"self\"}");
        var action = new Action("draw", payload);

        Assert.Equal("draw", action.Verb);
        Assert.Equal("self", action.Payload["target"]!.ToString());
    }

    [Fact]
    public void Constructor_WithEmptyPayloadObject_Allowed()
    {
        var action = new Action("end_turn", new JObject());
        Assert.Empty(action.Payload);
    }

    [Fact]
    public void Constructor_WithNullVerb_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Action(null!, new JObject()));
    }

    [Fact]
    public void Constructor_WithEmptyVerb_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Action("", new JObject()));
    }

    [Fact]
    public void Constructor_WithWhitespaceVerb_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Action("   ", new JObject()));
    }

    [Fact]
    public void Constructor_WithNullPayload_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new Action("draw", null!));
    }
}
