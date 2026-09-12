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

    private readonly FhSettingToggle _enabled = new("fhqol.splash.skip", true);

    private int _traced;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void d_atel_event_setup(uint event_id);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate char* d_atel_get_event_name(uint event_id);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate byte d_opening_screen_active();

    public NoSplashModule()
    {
        settings = new FhSettingsCategory("fhqol.splash", [_enabled]);
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
               && new FhMethodHandle<d_opening_screen_active>(new FhMethodLocation(EngineAddresses.OpeningScreenActive, 0)).hook(this, h_opening_screen_active);

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

        new FhMethodHandle<d_atel_event_setup>(new FhMethodLocation(EngineAddresses.AtelEventSetUp, 0))
            .chain_from(h_atel_event_setup).fnptr!(target);
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
