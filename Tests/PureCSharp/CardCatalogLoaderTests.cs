using System;
using System.IO;
using CardCore;
using CardCore.Catalog;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CardCore.PureTests;

public class CardCatalogLoaderTests
{
    private static readonly string FixtureRoot =
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Cards");

    private static string Fixture(string name) => Path.Combine(FixtureRoot, name);

    [Fact]
    public void LoadFromJson_MinimalDefinition_ProducesIdOnlyCard()
    {
        var json = File.ReadAllText(Fixture("valid_minimal.json"));
        var catalog = CardCatalogLoader.LoadFromJson(json);

        Assert.Equal(1, catalog.Count);
        Assert.True(catalog.Contains("x"));
    }

    [Fact]
    public void LoadFromJson_FullDefinition_ParsesEveryField()
    {
        var json = File.ReadAllText(Fixture("valid_full.json"));
        var catalog = CardCatalogLoader.LoadFromJson(json);

        var def = catalog.Get("reverie_muse");
        Assert.Equal("reverie muse", def.Name.Raw);
        Assert.Equal(3, def.Types.Count);
        Assert.Single(def.Costs);
        Assert.Equal("[dream]", def.Costs[0].Type);
        Assert.Single(def.Actions);
        Assert.Equal("draw", def.Actions[0].Verb);
        Assert.Equal("epic", def.Rarity);
        Assert.Equal("dreams of glory", def.Flavor.Raw);
    }

    [Fact]
    public void LoadFromJson_ArrayOfDefinitions_ProducesCatalog()
    {
        var json = "[ {\"id\":\"a\"}, {\"id\":\"b\"} ]";
        var catalog = CardCatalogLoader.LoadFromJson(json);

        Assert.Equal(2, catalog.Count);
        Assert.True(catalog.Contains("a"));
        Assert.True(catalog.Contains("b"));
    }

    [Fact]
    public void LoadFromJson_MissingId_Throws()
    {
        var json = File.ReadAllText(Fixture("invalid_missing_id.json"));
        Assert.Throws<CardCatalogLoadException>(() => CardCatalogLoader.LoadFromJson(json));
    }

    [Fact]
    public void LoadFromJson_UppercaseId_Throws()
    {
        var json = File.ReadAllText(Fixture("invalid_uppercase_id.json"));
        Assert.Throws<CardCatalogLoadException>(() => CardCatalogLoader.LoadFromJson(json));
    }

    [Fact]
    public void LoadFromJson_WhitespaceId_Throws()
    {
        var json = File.ReadAllText(Fixture("invalid_whitespace_id.json"));
        Assert.Throws<CardCatalogLoadException>(() => CardCatalogLoader.LoadFromJson(json));
    }

    [Fact]
    public void LoadFromJson_ActionWithoutVerb_Throws()
    {
        var json = File.ReadAllText(Fixture("invalid_action_no_verb.json"));
        Assert.Throws<CardCatalogLoadException>(() => CardCatalogLoader.LoadFromJson(json));
    }

    [Fact]
    public void LoadFromJson_AggregateError_ListsAllBadCards()
    {
        var json = "[ {\"id\":\"\"}, {\"id\":\"Foo\"} ]";

        var ex = Assert.Throws<CardCatalogLoadException>(() => CardCatalogLoader.LoadFromJson(json));
        Assert.Equal(2, ex.Errors.Count);
    }

    [Fact]
    public void LoadFromJson_UnpairedCost_LoadsWithWarning()
    {
        var json = File.ReadAllText(Fixture("warnings_unpaired_cost.json"));
        var catalog = CardCatalogLoader.LoadFromJson(json);

        Assert.True(catalog.Contains("unpaired"));
        Assert.NotEmpty(catalog.LoadWarnings);
    }

    [Fact]
    public void LoadFromStream_BehavesLikeLoadFromJson()
    {
        using var fs = File.OpenRead(Fixture("valid_minimal.json"));
        var catalog = CardCatalogLoader.LoadFromStream(fs);
        Assert.Equal(1, catalog.Count);
    }

    [Fact]
    public void LoadFromDirectory_LoadsEveryValidFile()
    {
        var temp = Path.Combine(Path.GetTempPath(), "cardcore_loader_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            File.WriteAllText(Path.Combine(temp, "a.json"), "{\"id\":\"a\"}");
            File.WriteAllText(Path.Combine(temp, "treasures.json"), "[ {\"id\":\"b\"}, {\"id\":\"c\"} ]");

            var catalog = CardCatalogLoader.LoadFromDirectory(temp);

            Assert.Equal(3, catalog.Count);
            Assert.True(catalog.Contains("a"));
            Assert.True(catalog.Contains("b"));
            Assert.True(catalog.Contains("c"));
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    [Fact]
    public void LoadFromDirectory_DuplicateIdAcrossFiles_Throws()
    {
        var temp = Path.Combine(Path.GetTempPath(), "cardcore_loader_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            File.WriteAllText(Path.Combine(temp, "a.json"), "{\"id\":\"a\"}");
            File.WriteAllText(Path.Combine(temp, "b.json"), "{\"id\":\"a\"}");

            Assert.Throws<CardCatalogLoadException>(() => CardCatalogLoader.LoadFromDirectory(temp));
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    [Fact]
    public void LoadDefinition_FromValidJObject_ReturnsCardDefinition()
    {
        var jo = JObject.Parse("{\"id\":\"alpha\",\"name\":{\"raw\":\"Alpha\"}}");
        var def = CardCatalogLoader.LoadDefinition(jo);
        Assert.Equal("alpha", def.Id);
        Assert.Equal("Alpha", def.Name.Raw);
    }

    [Fact]
    public void LoadDefinition_BadId_Throws()
    {
        var jo = JObject.Parse("{\"id\":\"Foo\"}");
        Assert.Throws<CardCatalogLoadException>(() => CardCatalogLoader.LoadDefinition(jo));
    }
}
