using Fahrenheit.Mods.Qol;
using Xunit;

public sealed class FocusInputNativeTests
{
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    public unsafe void XInputOnlyPreviouslyConnectedFailureInvalidatesRelease(bool before, bool after, bool canRelease)
    {
        var module = Blocked(0);
        byte* device = stackalloc byte[0x3a8];
        new Span<byte>(device, 0x3a8).Clear();
        device[0x69] = before ? (byte)1 : (byte)0;
        NativeDeviceFixture.NativePoll = ptr => *((byte*)ptr + 0x69) = after ? (byte)1 : (byte)0;
        module.XInputPoll((nint)device);
        module.Poll(0, 0);
        Assert.Equal(canRelease, module.TryGetVrrInputPolicy(1, out _, out _));
    }

    [Fact]
    public unsafe void XInputFailureCannotBeClearedByOtherDeviceOrThrowingRecovery()
    {
        var module = Blocked(0);
        byte* devices = stackalloc byte[2 * 0x3a8];
        new Span<byte>(devices, 2 * 0x3a8).Clear();
        devices[0x69] = 1;
        NativeDeviceFixture.NativePoll = ptr => *((byte*)ptr + 0x69) = 0;
        module.XInputPoll((nint)devices);
        NativeDeviceFixture.NativePoll = ptr => *((byte*)ptr + 0x69) = 1;
        module.XInputPoll((nint)(devices + 0x3a8));
        module.Poll(0, 0);
        Assert.False(module.TryGetVrrInputPolicy(1, out _, out _));
        var error = new InvalidOperationException("native");
        NativeDeviceFixture.NativePoll = ptr => { *((byte*)ptr + 0x69) = 1; throw error; };
        Assert.Same(error, Assert.Throws<InvalidOperationException>(() => module.XInputPoll((nint)devices)));
        module.Poll(0, 0);
        Assert.False(module.TryGetVrrInputPolicy(1, out _, out _));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public unsafe void XInputDisconnectZeroDoesNotRearmHeldButtonAndReconnectStillNeedsRelease(bool enabled)
    {
        var module = Blocked(0);
        module.Enabled = enabled;
        byte* device = stackalloc byte[0x3a8];
        new Span<byte>(device, 0x3a8).Clear();
        device[0x69] = 1;
        int calls = 0;
        nint expectedDevice = (nint)device;
        NativeDeviceFixture.NativePoll = ptr =>
        {
            Assert.Equal(expectedDevice, ptr);
            calls++;
            *((byte*)ptr + 0x69) = 0;
            module.Buttons[0] = 0;
        };
        module.XInputPoll(expectedDevice);
        NativeRefreshFixture.Native = (_, _, _) => { };
        module.Refresh(0, 0, 0);
        Assert.Equal(1, calls);
        Assert.False(module.TryGetVrrInputPolicy(1, out _, out _));

        NativeDeviceFixture.NativePoll = ptr => { *((byte*)ptr + 0x69) = 1; module.Buttons[0] = 0x800; };
        module.XInputPoll(expectedDevice);
        module.Refresh(0, 0, 0);
        module.Publish();
        Assert.Equal(0u, module.Buttons[0]);
        NativeDeviceFixture.NativePoll = ptr => { *((byte*)ptr + 0x69) = 1; module.Buttons[0] = 0; };
        module.XInputPoll(expectedDevice);
        module.Refresh(0, 0, 0);
        Assert.True(module.TryGetVrrInputPolicy(1, out _, out _));
        module.Poll(0, 0x800);
        module.Publish();
        Assert.Equal(0x800u, module.Buttons[0]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public unsafe void UnrelatedDeviceSuccessCannotClearFailedDeviceButMatchingRecoveryAllowsRelease(bool enabled)
    {
        var module = Blocked(0);
        module.Enabled = enabled;
        byte* devices = stackalloc byte[2 * 0x3a8];
        new Span<byte>(devices, 2 * 0x3a8).Clear();
        nint* table = stackalloc nint[10];
        table[9] = (nint)(delegate* unmanaged[Stdcall]<nint, uint, void*, int>)&NativeDeviceFixture.GetState;
        nint input = (nint)table;
        *(nint*)(devices + 0x14) = (nint)(&input);
        *(nint*)(devices + 0x3a8 + 0x14) = (nint)(&input);
        NativeDeviceFixture.State = new byte[0x110];
        Array.Fill(NativeDeviceFixture.State, (byte)0xff, 0x20, 4);
        NativeDeviceFixture.Result = -1;
        NativeDeviceFixture.NativePoll = ptr => *((byte*)ptr + 0x3a4) = 1;
        NativeDeviceFixture.NativePublish = _ => { };
        module.DevicePoll((nint)devices);
        NativeDeviceFixture.Result = 0;
        NativeDeviceFixture.NativePoll = ptr => *((byte*)ptr + 0x3a4) = 0;
        module.DevicePoll((nint)(devices + 0x3a8));
        module.Poll(0, 0);
        Assert.False(module.TryGetVrrInputPolicy(1, out _, out _));

        *(nint*)(devices + 0x14) = 0;
        module.DevicePoll((nint)devices);
        module.Poll(0, 0);
        Assert.False(module.TryGetVrrInputPolicy(1, out _, out _));

        *(nint*)(devices + 0x14) = (nint)(&input);
        module.DevicePoll((nint)devices);
        module.Poll(0, 0);
        Assert.True(module.TryGetVrrInputPolicy(1, out _, out _));
        module.Poll(0, 0x800);
        module.Publish();
        Assert.Equal(0x800u, module.Buttons[0]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public unsafe void InvalidOrReacquiredPhysicalReadCannotRearmFromSyntheticNativeZero(int result)
    {
        var module = Blocked(0);
        byte* device = stackalloc byte[0x3a8];
        new Span<byte>(device, 0x3a8).Clear();
        nint* table = stackalloc nint[10];
        table[9] = (nint)(delegate* unmanaged[Stdcall]<nint, uint, void*, int>)&NativeDeviceFixture.GetState;
        nint input = (nint)table;
        *(nint*)(device + 0x14) = (nint)(&input);
        NativeDeviceFixture.State = new byte[0x110];
        NativeDeviceFixture.State[0x30] = 0x80;
        Array.Fill(NativeDeviceFixture.State, (byte)0xff, 0x20, 4);
        NativeDeviceFixture.Result = result;
        NativeDeviceFixture.NativePoll = ptr => *((byte*)ptr + 0x3a4) = 1;
        NativeDeviceFixture.NativePublish = _ => { };
        module.DevicePoll((nint)device);
        module.Poll(0, 0);
        Assert.False(module.TryGetVrrInputPolicy(1, out _, out _));
        module.Poll(0, 0x800);
        module.Publish();
        Assert.Equal(0u, module.Buttons[0]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ActualHookForwardsIdentityOnceAndObservesOnlyAfterNativeReturn(int port)
    {
        var module = Blocked(port);
        int calls = 0;
        NativeRefreshFixture.Native = (actualPort, pad, buffer) =>
        {
            Assert.Equal(port, actualPort);
            Assert.Equal(0, pad);
            Assert.Equal((nint)0x1234, buffer);
            calls++;
            module.Buttons[port] = 0;
        };
        module.Refresh(port, 0, 0x1234);
        Assert.Equal(1, calls);
        Assert.True(module.TryGetVrrInputPolicy(1, out _, out _));
        module.Buttons[port] = 0x800;
        module.Publish();
        Assert.Equal(0x800u, module.Buttons[port]);
    }

    [Fact]
    public void FailedNativeRefreshCannotRearmFromPartiallyWrittenZero()
    {
        var module = Blocked(0);
        var error = new InvalidOperationException("native");
        NativeRefreshFixture.Native = (_, _, _) => { module.Buttons[0] = 0; throw error; };
        Assert.Same(error, Assert.Throws<InvalidOperationException>(() => module.Refresh(0, 0, 0)));
        Assert.False(module.TryGetVrrInputPolicy(1, out _, out _));
        module.Buttons[0] = 0x800;
        module.Publish();
        Assert.Equal(0u, module.Buttons[0]);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(2, 0)]
    [InlineData(0, 1)]
    public void UnsupportedNativeIdentityForwardsWithoutRearming(int port, int pad)
    {
        var module = Blocked(0);
        int calls = 0;
        NativeRefreshFixture.Native = (p, d, _) => { Assert.Equal(port, p); Assert.Equal(pad, d); calls++; };
        module.Refresh(port, pad, 0);
        Assert.Equal(1, calls);
        Assert.False(module.TryGetVrrInputPolicy(1, out _, out _));
    }

    private static FocusInputModule Blocked(int port)
    {
        var module = new FocusInputModule { Focused = false };
        module.Ready();
        module.Poll(port, 0x800);
        module.Publish();
        module.Focused = true;
        module.Publish();
        module.Publish();
        module.Publish();
        Assert.False(module.TryGetVrrInputPolicy(1, out _, out _));
        return module;
    }
}
