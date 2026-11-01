global using System.Runtime.CompilerServices;
global using System.Runtime.InteropServices;

namespace Fahrenheit.Mods.Qol;

public unsafe sealed partial class FocusInputModule
{
    private readonly NativeLogger _logger = new();
    public void Refresh(int port, int pad, nint buffer) => h_refresh_pad_input(port, pad, buffer);
    public void DevicePoll(nint device) => h_directinput_pad_poll(device);
    public void XInputPoll(nint device) => h_xinput_pad_poll(device);
    private sealed class NativeLogger { public void Error(string message) { } public void Info(string message) { } }
}

internal readonly record struct FhCallLocation(nint Address, int Offset);

internal sealed class FhCallHandle<T>(FhCallLocation location) where T : Delegate
{
    public T fnptr
    {
        get
        {
            Delegate native = location.Address == EngineAddresses.RefreshPadInput ? NativeRefreshFixture.Native
                : location.Address == EngineAddresses.DirectInputPadPoll || location.Address == EngineAddresses.XInputPadPoll ? NativeDeviceFixture.NativePoll
                : location.Address == EngineAddresses.InputDevicePublish ? NativeDeviceFixture.NativePublish
                : throw new InvalidOperationException("Unexpected native target.");
            if (location.Offset != 0) throw new InvalidOperationException("Unexpected native offset.");
            return (T)Delegate.CreateDelegate(typeof(T), native.Target, native.Method);
        }
    }
    public FhCallHandle<T> chain_from(T callback) => this;
    public bool hook(object owner, T callback) => throw new NotSupportedException("Native hook installation is outside the fixture.");
}

internal static unsafe class FhUtil
{
    public static T* ptr_at<T>(nint address) where T : unmanaged
        => throw new NotSupportedException("Executable memory is outside the fixture.");
}

internal static class NativeRefreshFixture
{
    [ThreadStatic] public static Action<int, int, nint> Native = null!;
}

internal static unsafe class NativeDeviceFixture
{
    [ThreadStatic] public static Action<nint> NativePoll = null!;
    [ThreadStatic] public static Action<nint> NativePublish = null!;
    [ThreadStatic] public static int Result;
    [ThreadStatic] public static byte[] State = null!;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    public static int GetState(nint input, uint length, void* state)
    {
        if (length != 0x110) return -1;
        State.AsSpan().CopyTo(new Span<byte>(state, 0x110));
        return Result;
    }
}
