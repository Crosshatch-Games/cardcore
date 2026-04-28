using CardCore;
using Xunit;

namespace CardCore.PureTests;

public class PlayerTests
{
    [Fact]
    public void NewPlayer_HasIdAndEmptyHand()
    {
        var player = new Player(id: 0);
        Assert.Equal(0, player.Id);
        Assert.Equal(0, player.Hand.Count);
    }

    [Fact]
    public void NegativeId_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Player(id: -1));
    }
}
