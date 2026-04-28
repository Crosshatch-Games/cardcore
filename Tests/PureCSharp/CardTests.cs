using CardCore;
using Xunit;

namespace CardCore.PureTests;

public class CardTests
{
    [Fact]
    public void Constructor_WithValidInput_SetsProperties()
    {
        var card = new Card(Id: 42, Name: "Copper");

        Assert.Equal(42, card.Id);
        Assert.Equal("Copper", card.Name);
    }

    [Fact]
    public void Constructor_WithNegativeId_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Card(Id: -1, Name: "Copper"));
    }

    [Fact]
    public void Constructor_WithNullName_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Card(Id: 1, Name: null!));
    }

    [Fact]
    public void Constructor_WithEmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Card(Id: 1, Name: ""));
    }
}
