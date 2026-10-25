using Fahrenheit.Mods.Qol;
using Xunit;

public sealed class MenuInputStateTests
{
    [Theory]
    [InlineData(0u, true)]
    [InlineData(4500u, true)]
    [InlineData(31500u, true)]
    [InlineData(9000u, false)]
    [InlineData(18000u, false)]
    [InlineData(27000u, false)]
    [InlineData(uint.MaxValue, false)]
    public void ChecksFreshPovAfterDeviceReacquisition(uint pov, bool up)
        => Assert.Equal(up, MenuInputState.IsPovUp(pov));
    [Fact]
    public void NeutralizesOnlyPhysicalAxesButtonsAndPov()
    {
        byte[] data = Enumerable.Repeat((byte)0x55, 0x3b0).ToArray();
        MenuInputState.NeutralizeDevice(data);
        var expected = Enumerable.Repeat((byte)0x55, data.Length).ToArray();
        foreach (int offset in new[] { 0x370, 0x374, 0x37c, 0x380 }) Array.Clear(expected, offset, 4);
        Array.Clear(expected, 0x398, 16);
        Assert.Equal(expected, data);
    }

    [Fact]
    public void RejectsShortDeviceBeforeChangingAnything()
    {
        byte[] data = Enumerable.Repeat((byte)0x55, 0x3a7).ToArray();
        Assert.Throws<ArgumentException>(() => MenuInputState.NeutralizeDevice(data));
        Assert.All(data, b => Assert.Equal(0x55, b));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(unchecked((int)0x8007001e), true)]
    [InlineData(unchecked((int)0x8007000c), true)]
    public void KeepsSuccessfulDeviceInput(int result, bool discard)
        => Assert.Equal(discard, MenuInputState.ShouldDiscard(result));
}
