using System.Collections.Generic;
using CardCore;
using CardCore.Catalog;
using Newtonsoft.Json;
using Xunit;

namespace CardCore.PureTests;

public class CardDefinitionMetadataTests
{
    private static CardDefinition Load(string json) =>
        CardCatalogLoader.LoadFromJson(json).Get("x");

    [Fact]
    public void UnknownField_LandsInMetadata()
    {
        var def = Load("{\"id\":\"x\",\"endOfTurn\":\"discard\"}");
        Assert.Equal("discard", def.Metadata["endOfTurn"]);
    }

    [Fact]
    public void NoExtraFields_MetadataIsEmptyNotNull()
    {
        var def = Load("{\"id\":\"x\"}");
        Assert.NotNull(def.Metadata);
        Assert.Empty(def.Metadata);
        Assert.Empty(new CardDefinition("x").Metadata);
    }

    [Fact]
    public void KnownFields_DoNotAppearInMetadata()
    {
        var def = Load("{\"id\":\"x\",\"name\":\"N\",\"types\":[\"a\"],\"costs\":[],\"rewards\":[],"
            + "\"thresholds\":[],\"actions\":[],\"targets\":[],\"back\":\"b\",\"rarity\":\"r\",\"flavor\":\"f\"}");
        Assert.Empty(def.Metadata);
    }

    [Fact]
    public void MultipleUnknownFields_AllCaptured_KeysVerbatim()
    {
        var def = Load("{\"id\":\"x\",\"Foo\":\"a\",\"bar\":2,\"baz\":true}");
        Assert.Equal(3, def.Metadata.Count);
        Assert.Equal("a", def.Metadata["Foo"]);
        Assert.Equal("2", def.Metadata["bar"]);
        Assert.Equal("true", def.Metadata["baz"]);
    }

    [Fact]
    public void ObjectAndArrayValues_StoredAsRawJson_AndRoundTrip()
    {
        var def = Load("{\"id\":\"x\",\"obj\":{\"a\":1,\"b\":[1,2]},\"arr\":[1,\"two\"]}");
        Assert.Equal("{\"a\":1,\"b\":[1,2]}", def.Metadata["obj"]);
        Assert.Equal("[1,\"two\"]", def.Metadata["arr"]);

        var json = JsonConvert.SerializeObject(def);
        var revived = JsonConvert.DeserializeObject<CardDefinition>(json)!;
        Assert.Equal(def.Metadata["obj"], revived.Metadata["obj"]);
        Assert.Equal(def.Metadata["arr"], revived.Metadata["arr"]);
    }

    [Fact]
    public void Constructor_CopiesMetadata()
    {
        var src = new Dictionary<string, string> { ["k"] = "v" };
        var def = new CardDefinition("x", Metadata: src);
        src["k"] = "changed";
        Assert.Equal("v", def.Metadata["k"]);
    }
}
