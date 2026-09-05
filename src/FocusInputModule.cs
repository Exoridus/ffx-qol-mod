namespace Fahrenheit.Mods.Qol;

/// <summary>
///     Keeps the pad neutral while the window does not own input.
///
///     <para>
///     The symptom is a menu cursor that walks upward, and a character that walks off on the field,
///     from the moment the window loses focus until it regains it - with nothing held down. That
///     rules out a frozen input state, which with nothing pressed would read as neutral, and points
///     at a zeroed analog byte: the pad's analog axes are bytes whose rest position is 0x80, so a
///     zero is full deflection rather than centre.
///     </para>
///
///     <para>
///     The engine names that rest position itself. <c>FUN_00888f70</c> and <c>FUN_00888fa0</c> are
///     its own neutral writers and both store <c>0x80808080</c> into the four analog bytes, which is
///     also what the float-to-byte conversion produces: <c>FUN_00889a10</c> is
///     <c>-0x80 - (char)round(f * -127.0)</c>, so 0.0f maps to 0x80 and the usable range is 0x01 to
///     0xFF around it. This module writes the same word rather than a value of its own.
///     </para>
///
///     <para>
///     The hook sits on <c>TkScanControler</c>, which the main loop calls once per frame and which
///     pushes one sample per port into a four-slot history ring. Neutralising the pad record before
///     that push is what matters: the script-facing snapshot in <c>FUN_00871d10</c> ORs the ring over
///     every frame since the last sync, so a bad sample keeps being reported as held rather than
///     lapsing after one frame.
///     </para>
///
///     <para>
///     Focus comes from Win32. The engine has no window-active flag - <c>GetForegroundWindow</c>,
///     <c>GetActiveWindow</c> and <c>WM_ACTIVATE</c> appear nowhere in the decompilation, the window
///     belongs to Phyre's <c>PApplication</c>, and the global a previous attempt used for it,
///     <c>ODBegin</c>, is written only by <c>TOBtlCtrlLuluLimitWindow</c>: it is Lulu's overdrive
///     input window, not the window state.
///     </para>
/// </summary>
[FhLoad(FhGameId.FFX)]
public unsafe sealed class FocusInputModule : FhModule
{
    private readonly FhSettingToggle _neutralize = new("fhqol.focus.neutralize", true);
    private readonly FhSettingToggle _survey     = new("fhqol.focus.survey",     false);

    private long _scans;
    private long _neutralized;
    private int  _samples_logged;
    private bool _was_focused = true;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void d_tk_scan_controler();

    public FocusInputModule()
    {
        settings = new FhSettingsCategory("fhqol.focus", [_neutralize, _survey]);
    }

    public override bool init(FhModContext mod_context, FileStream global_state_file)
    {
        if (!new FhMethodHandle<d_tk_scan_controler>(new FhMethodLocation(EngineAddresses.TkScanControler, 0)).hook(this, h_tk_scan_controler))
        {
            _logger.Error("[QoL] Could not hook TkScanControler; the focus-loss drift stays.");
            return false;
        }

        _logger.Info("[QoL] Focus neutralisation hooked on TkScanControler.");
        return true;
    }

    private void h_tk_scan_controler()
    {
        _scans++;

        bool focused = WindowOwnsInput();

        if (!focused && _neutralize.get())
        {
            for (var port = 0; port < EngineAddresses.PadRecordCount; port++)
                NeutralizePad(PadRecord(port));

            _neutralized++;
        }

        if (_survey.get() && focused && _samples_logged < 8)
        {
            var pad = PadRecord(0);
            _samples_logged++;
            _logger.Info($"[QoL] Pad 0 while focused: analog {*(uint*)(pad + 0x84):X8} {*(uint*)(pad + 0x90):X8} "
                       + $"buttons {*(ushort*)(pad + 0x98):X4} {*(ushort*)(pad + 0x9a):X4}");
        }

        if (focused != _was_focused)
        {
            _was_focused = focused;
            _logger.Info(focused
                ? $"[QoL] Focus regained after {_neutralized} neutralised scan(s)."
                : "[QoL] Focus lost; holding the pad neutral.");
        }

        new FhMethodHandle<d_tk_scan_controler>(new FhMethodLocation(EngineAddresses.TkScanControler, 0))
            .chain_from(h_tk_scan_controler).fnptr!();
    }

    /// <summary>
    ///     The pad record for one port. FUN_00888e30 is pure address arithmetic on a static array,
    ///     <c>(port + pad) * 0x100 + 0x01330248</c>, so there is nothing to call.
    /// </summary>
    private static byte* PadRecord(int port) =>
        FhUtil.ptr_at<byte>(EngineAddresses.PadRecordBase + port * EngineAddresses.PadRecordStride);

    /// <summary>
    ///     Exactly what FUN_00888f70 writes. Kept field for field rather than reduced to the two
    ///     analog words: the two zeroed words between them are part of the engine's own idea of a
    ///     neutral pad, and guessing which of the four matter is how a layout gets mis-read.
    /// </summary>
    private static void NeutralizePad(byte* pad)
    {
        *(uint*)(pad + 0x84) = 0x80808080;
        *(uint*)(pad + 0x88) = 0;
        *(uint*)(pad + 0x8c) = 0;
        *(uint*)(pad + 0x90) = 0x80808080;

        // The button words FUN_00889700 reads out of the record on its way into the ring slot.
        *(ushort*)(pad + 0x98) = 0;
        *(ushort*)(pad + 0x9a) = 0;
    }

    /// <summary>
    ///     Whether the foreground window belongs to this process. Comparing against the process's
    ///     own MainWindowHandle would be narrower and wrong: the game has more than one top-level
    ///     window over its lifetime, and the handle is cached by the framework.
    /// </summary>
    private static bool WindowOwnsInput()
    {
        nint foreground = GetForegroundWindow();
        if (foreground == 0) return false;

        _ = GetWindowThreadProcessId(foreground, out uint pid);
        return pid == CurrentProcessId;
    }

    private static readonly uint CurrentProcessId = (uint)Environment.ProcessId;

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);
}
