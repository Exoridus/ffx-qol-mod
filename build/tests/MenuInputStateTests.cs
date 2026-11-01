using Fahrenheit.Mods.Qol;
using Xunit;

public sealed class MenuInputStateTests
{
    [Theory]
    [InlineData(0u, 0x00000001u)]
    [InlineData(4500u, 0x00000101u)]
    [InlineData(9000u, 0x00000100u)]
    [InlineData(13500u, 0x00010100u)]
    [InlineData(18000u, 0x00010000u)]
    [InlineData(22500u, 0x01010000u)]
    [InlineData(27000u, 0x01000000u)]
    [InlineData(31500u, 0x01000001u)]
    [InlineData(uint.MaxValue, 0u)]
    public void PhysicalValidationMatchesNativeAxesButtonsAndPov(uint pov, uint directions)
    {
        byte[] physical = new byte[0x110], device = new byte[0x3a8];
        BitConverter.GetBytes(pov).CopyTo(physical, 0x20);
        BitConverter.GetBytes(directions).CopyTo(device, 0x3a4);
        physical[0] = device[0x370] = 21;
        physical[4] = device[0x37c] = 42;
        physical[8] = device[0x374] = 63;
        physical[0x14] = device[0x380] = 84;
        for (int i = 0; i < 12; i++) { physical[0x30 + i] = 0x80; device[0x398 + i] = 1; }
        Assert.True(MenuInputState.MatchesPublishedState(physical, device));
        foreach (int offset in new[] { 0x370, 0x37c, 0x374, 0x380, 0x398, 0x39d, 0x3a3, 0x3a4, 0x3a5, 0x3a6, 0x3a7 })
        {
            byte saved = device[offset];
            device[offset] ^= 1;
            Assert.False(MenuInputState.MatchesPublishedState(physical, device));
            device[offset] = saved;
        }
    }

    [Fact]
    public void PhysicalValidationRejectsIncompleteInput()
    {
        Assert.Throws<ArgumentException>(() => MenuInputState.MatchesPublishedState(new byte[0x10f], new byte[0x3a8]));
        Assert.Throws<ArgumentException>(() => MenuInputState.MatchesPublishedState(new byte[0x110], new byte[0x3a7]));
    }

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
