namespace Fahrenheit.Mods.Qol;

public unsafe sealed partial class FocusInputModule
{
    private long _invalidPadPolls;

    [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
    private delegate void d_pad_device_update(nint device);

    private bool init_native_pad_focus()
    {
        ReadOnlySpan<byte> poll = [0x55, 0x8b, 0xec, 0x81, 0xec, 0x14, 0x01, 0x00];
        ReadOnlySpan<byte> publish = [0x56, 0x8b, 0x71, 0x0c, 0x57, 0x8d, 0x79, 0x0c];
        if (!new ReadOnlySpan<byte>(FhUtil.ptr_at<byte>(EngineAddresses.DirectInputPadPoll), poll.Length).SequenceEqual(poll)
            || !new ReadOnlySpan<byte>(FhUtil.ptr_at<byte>(EngineAddresses.InputDevicePublish), publish.Length).SequenceEqual(publish)
            || !new FhCallHandle<d_pad_device_update>(new FhCallLocation(EngineAddresses.DirectInputPadPoll, 0))
                .hook(this, h_directinput_pad_poll))
        {
            _logger.Error("[QoL] DirectInput focus repair refused: unexpected poll/publish code or unavailable hook.");
            return false;
        }
        _logger.Info("[QoL] DirectInput pad focus repair active: failed state reads cannot publish a phantom POV-up press.");
        return true;
    }

    [UnmanagedCallConv(CallConvs = [typeof(CallConvThiscall)])]
    private void h_directinput_pad_poll(nint device)
    {
        new FhCallHandle<d_pad_device_update>(new FhCallLocation(EngineAddresses.DirectInputPadPoll, 0))
            .chain_from(h_directinput_pad_poll).fnptr!(device);
        if (!_neutralize.get() || *(byte*)(device + 0x3a4) == 0) return;
        nint input = *(nint*)(device + 0x14);
        if (input == 0) return;
        byte* state = stackalloc byte[0x110];
        var getState = (delegate* unmanaged[Stdcall]<nint, uint, void*, int>)(*(nint**)input)[9];
        int result = getState(input, 0x110, state);
        uint pov = result < 0 ? uint.MaxValue : *(uint*)(state + 0x20);
        if (MenuInputState.ShouldDiscard(result))
            MenuInputState.NeutralizeDevice(new Span<byte>((void*)device, 0x3a8));
        else if (!MenuInputState.IsPovUp(pov))
            // Acquire can succeed after the native poll published its zeroed failure buffer.
            *(byte*)(device + 0x3a4) = 0;
        else return;
        new FhCallHandle<d_pad_device_update>(new FhCallLocation(EngineAddresses.InputDevicePublish, 0)).fnptr!(device);
        if (++_invalidPadPolls <= 4)
            _logger.Info($"[QoL] Removed phantom DirectInput POV-up: HRESULT=0x{result:X8}, fresh POV=0x{pov:X8}.");
    }
}
