using System;
using System.Collections.Generic;
using CardCore;
using CardCore.Catalog;
using Xunit;

namespace CardCore.PureTests;

public class CardCatalogTests
{
    [Fact]
    public void Constructor_WithDefinitions_ExposesCount()
    {
        var defs = new[] { new CardDefinition("a"), new CardDefinition("b") };
        var catalog = new CardCatalog(defs);

        Assert.Equal(2, catalog.Count);
    }

    [Fact]
    public void Constructor_WithDuplicateId_Throws()
    {
        var defs = new[] { new CardDefinition("a"), new CardDefinition("a") };
        Assert.Throws<ArgumentException>(() => new CardCatalog(defs));
    }

    [Fact]
    public void Get_WithKnownId_ReturnsDefinition()
    {
        var def = new CardDefinition("copper");
        var catalog = new CardCatalog(new[] { def });

        Assert.Same(def, catalog.Get("copper"));
    }

    [Fact]
    public void Get_WithUnknownId_Throws()
    {
        var catalog = new CardCatalog(new[] { new CardDefinition("copper") });
        Assert.Throws<KeyNotFoundException>(() => catalog.Get("missing"));
    }

    [Fact]
    public void TryGet_WithKnownId_ReturnsTrue()
    {
        var def = new CardDefinition("copper");
        var catalog = new CardCatalog(new[] { def });

        Assert.True(catalog.TryGet("copper", out var found));
        Assert.Same(def, found);
    }

    [Fact]
    public void TryGet_WithUnknownId_ReturnsFalse()
    {
        var catalog = new CardCatalog(new[] { new CardDefinition("copper") });

        Assert.False(catalog.TryGet("missing", out var found));
        Assert.Null(found);
    }

    [Fact]
    public void Contains_RespectsMembership()
    {
        var catalog = new CardCatalog(new[] { new CardDefinition("copper") });
        Assert.True(catalog.Contains("copper"));
        Assert.False(catalog.Contains("missing"));
    }

    [Fact]
    public void LoadWarnings_DefaultIsEmpty()
    {
        var catalog = new CardCatalog(new[] { new CardDefinition("a") });
        Assert.Empty(catalog.LoadWarnings);
    }

    [Fact]
    public void LoadWarnings_PopulatedConstructor_PreservesWarnings()
    {
        var warnings = new[] { "warning 1", "warning 2" };
        var catalog = new CardCatalog(new[] { new CardDefinition("a") }, warnings);

        Assert.Equal(2, catalog.LoadWarnings.Count);
        Assert.Equal("warning 1", catalog.LoadWarnings[0]);
    }
}
