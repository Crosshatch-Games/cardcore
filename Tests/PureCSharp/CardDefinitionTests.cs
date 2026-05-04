using System;
using System.Collections.Generic;
using CardCore;
using CardCore.Markdown;
using Xunit;
using Action = CardCore.Action;

namespace CardCore.PureTests;

public class CardDefinitionTests
{
    [Fact]
    public void Constructor_WithOnlyId_SetsIdAndDefaultsEverythingElse()
    {
        var def = new CardDefinition("copper");

        Assert.Equal("copper", def.Id);
        Assert.Equal(MarkdownText.Empty, def.Name);
        Assert.Empty(def.Types);
        Assert.Empty(def.Costs);
        Assert.Empty(def.Rewards);
        Assert.Empty(def.Thresholds);
        Assert.Empty(def.Actions);
        Assert.Empty(def.Targets);
        Assert.Null(def.Back);
        Assert.Null(def.Rarity);
        Assert.Equal(MarkdownText.Empty, def.Flavor);
    }

    [Fact]
    public void Constructor_WithAllFields_SetsThemAll()
    {
        var def = new CardDefinition(
            "reverie_muse",
            Name: MarkdownParser.Parse("reverie muse"),
            Types: new[] { "lifebound", "dream", "hero" },
            Costs: new[] { new CurrencyAmount(3, "dream") },
            Rewards: new[] { new CurrencyAmount(2, "[star]") },
            Thresholds: Array.Empty<CurrencyAmount>(),
            Actions: Array.Empty<Action>(),
            Targets: Array.Empty<MarkdownText>(),
            Back: null,
            Rarity: "epic",
            Flavor: MarkdownParser.Parse("dreams of glory")
        );

        Assert.Equal("reverie_muse", def.Id);
        Assert.Equal("reverie muse", def.Name.Raw);
        Assert.Equal(3, def.Types.Count);
        Assert.Single(def.Costs);
        Assert.Single(def.Rewards);
        Assert.Equal("epic", def.Rarity);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithMissingId_Throws(string? id)
    {
        Assert.Throws<ArgumentException>(() => new CardDefinition(id!));
    }

    [Theory]
    [InlineData("Foo")]   // uppercase
    [InlineData("FOO")]
    [InlineData("foo BAR")]
    public void Constructor_WithUppercaseId_Throws(string id)
    {
        Assert.Throws<ArgumentException>(() => new CardDefinition(id));
    }

    [Theory]
    [InlineData("foo bar")]   // whitespace
    [InlineData(" foo")]
    [InlineData("foo ")]
    [InlineData("foo\tbar")]
    public void Constructor_WithWhitespaceInId_Throws(string id)
    {
        Assert.Throws<ArgumentException>(() => new CardDefinition(id));
    }

    [Fact]
    public void Constructor_WithUnderscoreAndDigitsInId_Allowed()
    {
        var def = new CardDefinition("level_2");
        Assert.Equal("level_2", def.Id);
    }

    [Fact]
    public void Equality_SameId_ReturnsTrueAcrossInstances()
    {
        var a = new CardDefinition("copper");
        var b = new CardDefinition("copper");
        Assert.Equal(a, b);
    }
}
