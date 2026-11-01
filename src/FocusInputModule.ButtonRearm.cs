namespace Fahrenheit.Mods.Qol;

public unsafe sealed partial class FocusInputModule
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void d_refresh_pad_input(int port, int pad, nint buffer);

    private bool init_focus_button_rearming()
    {
        ReadOnlySpan<byte> refresh = [0x55, 0x8b, 0xec, 0x8b, 0x4d, 0x08, 0x53, 0x56, 0x57, 0x8b, 0x7d, 0x0c];
        if (!new ReadOnlySpan<byte>(FhUtil.ptr_at<byte>(EngineAddresses.RefreshPadInput), refresh.Length).SequenceEqual(refresh)
            || !new FhCallHandle<d_refresh_pad_input>(new FhCallLocation(EngineAddresses.RefreshPadInput, 0))
                .hook(this, h_refresh_pad_input))
        {
            _logger.Error("[QoL] Focus button rearming refused: unexpected native refresh code or unavailable hook.");
            return false;
        }
        return true;
    }

    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private void h_refresh_pad_input(int port, int pad, nint buffer)
    {
        new FhCallHandle<d_refresh_pad_input>(new FhCallLocation(EngineAddresses.RefreshPadInput, 0))
            .chain_from(h_refresh_pad_input).fnptr!(port, pad, buffer);
        if (port is < 0 or >= EngineAddresses.PadRecordCount || pad != 0) return;
        observe_focus_buttons(port, read_pad_buttons(port));
    }
}
