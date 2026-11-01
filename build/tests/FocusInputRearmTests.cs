using Fahrenheit.Mods.Qol;
using Xunit;

public sealed class FocusInputRearmTests
{
    [Fact]
    public void ContinuousHoldDoesNotBecomeFreshPressAfterFocusNeutralization()
    {
        var module = new FocusInputModule();
        module.Ready();
        bool previous = true;
        int freshPresses = 0;
        module.Native = () =>
        {
            bool held = (module.Buttons[0] & 0x800) != 0;
            if (held && !previous) freshPresses++;
            previous = held;
        };
        foreach (bool focused in new[] { true, false, true, true, true, true, true })
        {
            module.Focused = focused;
            module.Poll(0, 0x08000800);
            module.Publish();
        }
        Assert.Equal(0, freshPresses);
        Assert.Equal(7, module.NativeCalls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ReleaseMustComeFromNativeRefreshAndDoesNotBlockIndependentButtons(int port)
    {
        var module = RefocusWithHold(port, 0x08000800);
        Assert.Equal(0u, module.Buttons[port]);
        Assert.False(module.TryGetVrrInputPolicy(1, out long blocked, out bool neutralized));
        Assert.False(neutralized);
        for (int i = 0; i < 8; i++) module.Publish();
        Assert.False(module.TryGetVrrInputPolicy(1, out long repeated, out _));
        Assert.Equal(blocked, repeated);

        module.Poll(port, 0x08002800);
        module.Publish();
        Assert.Equal(0x2000u, module.Buttons[port]);
        module.Poll(port, 0);
        module.Publish();
        Assert.True(module.TryGetVrrInputPolicy(1, out long released, out _));
        Assert.True(released > blocked);
        module.Poll(port, 0x08000800);
        module.Publish();
        Assert.Equal(0x08000800u, module.Buttons[port]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PressDuringFocusLossRemainsBlockedUntilObservedFocusedRelease(bool releaseBeforeRegain)
    {
        var module = new FocusInputModule { Focused = false };
        module.Ready();
        module.Poll(0, 0x800);
        module.Publish();
        module.Poll(0, releaseBeforeRegain ? 0u : 0x800u);
        module.Publish();
        module.Focused = true;
        for (int i = 0; i < 3; i++)
        {
            module.Poll(0, releaseBeforeRegain ? 0u : 0x800u);
            module.Publish();
            Assert.Equal(0u, module.Buttons[0]);
        }
        module.Poll(0, 0);
        module.Publish();
        module.Poll(0, 0x800);
        module.Publish();
        Assert.Equal(0x800u, module.Buttons[0]);
    }

    [Fact]
    public void EachPortRearmsIndependentlyAndRepeatedFocusLossStartsNewQuarantine()
    {
        var module = RefocusWithHold(0, 0x800);
        module.Poll(1, 0x20002000);
        module.Publish();
        Assert.Equal(0x20002000u, module.Buttons[1]);
        module.Focused = false;
        module.Publish();
        module.Focused = true;
        for (int i = 0; i < 4; i++)
        {
            module.Poll(0, 0x800);
            module.Poll(1, 0x20002000);
            module.Publish();
            Assert.Equal(new uint[2], module.Buttons);
        }
        module.Poll(0, 0);
        module.Publish();
        module.Poll(0, 0x800);
        module.Poll(1, 0x20002000);
        module.Publish();
        Assert.Equal(new uint[] { 0x800, 0 }, module.Buttons);
    }

    [Fact]
    public void QueryOnlyFocusCycleCannotEscapeQuarantineOrConsumeGrace()
    {
        var module = new FocusInputModule();
        module.Ready();
        module.Focused = false;
        Assert.True(module.TryGetVrrInputPolicy(1, out long lost, out bool neutralized));
        Assert.True(neutralized);
        module.Focused = true;
        for (int i = 0; i < 8; i++)
        {
            Assert.False(module.TryGetVrrInputPolicy(1, out _, out neutralized));
            Assert.False(neutralized);
            Assert.Equal(0, module.Grace);
        }
        module.Poll(0, 0x800);
        module.Publish();
        Assert.Equal(0u, module.Buttons[0]);
        Assert.False(module.TryGetVrrInputPolicy(1, out long blocked, out _));
        Assert.True(blocked > lost);
    }

    [Fact]
    public void OrdinaryPressesAndUnusedPortRemainAvailableWithoutFocusTransition()
    {
        var module = new FocusInputModule();
        module.Ready();
        foreach (uint buttons in new[] { 0u, 0x800u, 0x08000800u, 0u, uint.MaxValue })
        {
            module.Poll(0, buttons);
            module.Publish();
            Assert.Equal(buttons, module.Buttons[0]);
            Assert.True(module.TryGetVrrInputPolicy(1, out _, out _));
        }
    }

    [Fact]
    public void DisabledPolicyReturnsNativeInputAndInvalidatesPriorOwnership()
    {
        var module = RefocusWithHold(0, 0x800);
        module.TryGetVrrInputPolicy(1, out long blocked, out _);
        module.Enabled = false;
        module.Poll(0, 0x800);
        module.Publish();
        Assert.Equal(0x800u, module.Buttons[0]);
        Assert.True(module.TryGetVrrInputPolicy(1, out long disabled, out _));
        Assert.True(disabled > blocked);
    }

    private static FocusInputModule RefocusWithHold(int port, uint buttons)
    {
        var module = new FocusInputModule { Focused = false };
        module.Ready();
        module.Poll(port, buttons);
        module.Publish();
        module.Focused = true;
        for (int i = 0; i < 3; i++)
        {
            module.Poll(port, buttons);
            module.Publish();
        }
        return module;
    }
}
