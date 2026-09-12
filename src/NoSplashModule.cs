namespace Fahrenheit.Mods.Qol;

/// <summary>
///     Takes the boot sequence straight to the title screen: the opening movie is ended on its
///     first poll, and the two splash events that follow it are redirected to the title room.
/// </summary>
/// <remarks>
///     Nothing in the boot sequence is an FMV, so there is no video to skip. The opening screen is
///     a Flash movie and the two screens after it are ATEL events.
/// </remarks>
[FhLoad(FhGameId.FFX)]
public unsafe sealed class NoSplashModule : FhModule
{
    private const ushort TitleRoomId     = 23;   // test20
    private const uint   MemochekEventId = 348;
    private const uint   LoopdemoEventId = 349;

    private readonly FhSettingToggle _enabled    = new("fhqol.splash.skip", true);
    private readonly FhSettingToggle _no_attract = new("fhqol.splash.no_attract", true);

    private int  _traced;
    private uint _current_event;
    private long _idle_resets;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void d_atel_event_setup(uint event_id);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate char* d_atel_get_event_name(uint event_id);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate byte d_opening_screen_active();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate ushort d_atel_pressed_buttons();

    public NoSplashModule()
    {
        settings = new FhSettingsCategory("fhqol.splash", [_enabled, _no_attract]);
    }

    public override bool init(FhModContext mod_context, FileStream global_state_file)
    {
        // Read once at init: the hooks are installed or they are not, and toggling the setting
        // mid-run cannot un-skip a boot sequence that has already happened.
        if (!_enabled.get())
        {
            _logger.Info("[QoL] Splash skip disabled by config; the boot sequence and the opening demo play.");
            return true;
        }

        bool ok = new FhMethodHandle<d_atel_event_setup>(new FhMethodLocation(EngineAddresses.AtelEventSetUp, 0)).hook(this, h_atel_event_setup)
               && new FhMethodHandle<d_opening_screen_active>(new FhMethodLocation(EngineAddresses.OpeningScreenActive, 0)).hook(this, h_opening_screen_active)
               && new FhMethodHandle<d_atel_pressed_buttons>(new FhMethodLocation(EngineAddresses.AtelPressedButtons, 0)).hook(this, h_atel_pressed_buttons);

        _logger.Info(ok ? "[QoL] Splash skip armed." : "[QoL] Splash skip could not install all of its hooks.");
        return ok;
    }

    /// <summary>Redirects the splash events to the title room and leaves everything else alone.</summary>
    private void h_atel_event_setup(uint event_id)
    {
        string name = event_name(event_id);

        if (_traced < 8)
        {
            _traced++;
            _logger.Info($"[QoL] Boot event {event_id} ({name}).");
        }

        uint target = is_splash(event_id, name) ? TitleRoomId : event_id;
        if (target != event_id) _logger.Info($"[QoL] Redirected {name} ({event_id}) to test20 ({TitleRoomId}).");

        _current_event = target;

        new FhMethodHandle<d_atel_event_setup>(new FhMethodLocation(EngineAddresses.AtelEventSetUp, 0))
            .chain_from(h_atel_event_setup).fnptr!(target);
    }

    /// <summary>
    ///     Keeps the title screen's idle timer from running out, which is what otherwise starts the
    ///     attract demo.
    ///
    ///     <para>
    ///     Redirecting the attract event is not enough to make the title screen idle quietly. The
    ///     timer lives in the title script itself: one of its workers counts a private variable up
    ///     once per frame while nothing is pressed and the menu selection does not move, resets it
    ///     to zero on any input, and at 1200 frames - 40 seconds at 30 Hz - fades the music out over
    ///     60 frames, fades to black over 30, waits 30 and only then asks for the next room. The
    ///     fade is script work that happens before the room change, so intercepting the room change
    ///     leaves the fade in place: that is the black dip you see even with the demo suppressed.
    ///     </para>
    ///
    ///     <para>
    ///     The counter is only ever reset, never inspected, so answering its input query with a
    ///     non-zero word holds it at zero and the timer never expires. That query is the ATEL getter
    ///     hooked here, and the title script asks it in exactly one place - this loop - so nothing
    ///     else in the title screen can see the answer. Everywhere outside the title room the
    ///     original value is passed through untouched, which matters because thirteen other scripts
    ///     in the corpus read the same getter.
    ///     </para>
    /// </summary>
    private ushort h_atel_pressed_buttons()
    {
        ushort actual = new FhMethodHandle<d_atel_pressed_buttons>(new FhMethodLocation(EngineAddresses.AtelPressedButtons, 0))
            .chain_from(h_atel_pressed_buttons).fnptr!();

        if (actual != 0 || !_no_attract.get() || _current_event != TitleRoomId) return actual;

        // Any non-zero word will do: the caller tests it for zero and discards it.
        if (++_idle_resets == 1) _logger.Info("[QoL] Holding the title screen's idle timer at zero; the attract demo will not start.");
        return 1;
    }

    /* Reporting the opening movie as finished ends the render loop before its first pass. The loop's
     * only side effect, the deferred boot task, is issued again after it under the same guard, so
     * skipping the loop does not drop it. The engine takes the same shortcut itself when started
     * with `_ECalm`. */
    private byte h_opening_screen_active() => 0;

    private static bool is_splash(uint event_id, string name)
        => event_id == MemochekEventId
        || event_id == LoopdemoEventId
        || string.Equals(name, "memochek", StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, "loopdemo", StringComparison.OrdinalIgnoreCase);

    private static string event_name(uint event_id)
    {
        try
        {
            char* p = new FhMethodHandle<d_atel_get_event_name>(new FhMethodLocation(EngineAddresses.AtelGetEventName, 0)).fnptr!(event_id);
            return p == null ? string.Empty : Marshal.PtrToStringAnsi((nint)p) ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
