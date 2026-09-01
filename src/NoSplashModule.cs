namespace Fahrenheit.Mods.Qol;

/// <summary>
///     Takes the boot sequence straight to the title screen: the splash events are redirected, the
///     Japan-logo gate is answered no, and the boot videos are skipped without input.
/// </summary>
[FhLoad(FhGameId.FFX)]
public unsafe sealed class NoSplashModule : FhModule
{
    private const ushort TitleRoomId     = 23;   // test20
    private const uint   MemochekEventId = 348;
    private const uint   LoopdemoEventId = 349;

    /// <summary>
    ///     How long the boot video skip stays armed. Measured in real time rather than frames: a
    ///     frame budget halves in wall-clock terms as soon as the game runs at 60 Hz, which cut the
    ///     window short and let the later boot videos through.
    /// </summary>
    private static readonly TimeSpan BootSkipWindow = TimeSpan.FromSeconds(20);

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private long  _boot_skips;
    private int   _traced;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void d_atel_event_setup(uint event_id);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate char* d_atel_get_event_name(uint event_id);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int d_need_show_japan_logo();

    [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
    private delegate void d_fmv_skip_poll(nint ptr_this);

    public override bool init(FhModContext mod_context, FileStream global_state_file)
    {
        if (!QolConfig.Load(QolConfig.ResolvePath()).SkipSplash)
        {
            _logger.Info("[QoL] Splash skip disabled by config; the boot sequence and the opening demo play.");
            return true;
        }

        bool ok = new FhMethodHandle<d_atel_event_setup>(new FhMethodLocation(EngineAddresses.AtelEventSetUp, 0)).hook(this, h_atel_event_setup)
               && new FhMethodHandle<d_need_show_japan_logo>(new FhMethodLocation(EngineAddresses.NeedShowJapanLogo, 0)).hook(this, h_need_show_japan_logo)
               && new FhMethodHandle<d_fmv_skip_poll>(new FhMethodLocation(EngineAddresses.FmvSkipPoll, 0)).hook(this, h_fmv_skip_poll);

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

    /// <summary>Answers the logo gate no while the boot sequence is still running.</summary>
    private int h_need_show_japan_logo()
    {
        if (!gameplay_ready()) return 0;

        return new FhMethodHandle<d_need_show_japan_logo>(new FhMethodLocation(EngineAddresses.NeedShowJapanLogo, 0))
            .chain_from(h_need_show_japan_logo).fnptr!();
    }

    /* The engine polls Triangle to arm a skip and Cross to commit it. Inside the boot window the
     * commit is performed directly instead: the sentinels below are what the native commit path
     * writes, so the original must not run afterwards. */
    private void h_fmv_skip_poll(nint fmv)
    {
        FhMethodHandle<d_fmv_skip_poll> orig =
            new FhMethodHandle<d_fmv_skip_poll>(new FhMethodLocation(EngineAddresses.FmvSkipPoll, 0)).chain_from(h_fmv_skip_poll);

        if (_clock.Elapsed > BootSkipWindow || fmv == 0) { orig.fnptr!(fmv); return; }

        // Only act while a movie is actually playing, or the sentinels land in an idle manager.
        if (FhUtil.get_at<int>(EngineAddresses.GMoviePlay) != 1) { orig.fnptr!(fmv); return; }

        byte* p = (byte*)fmv;
        if (p[0x6d0] == 0 || p[0x6d2] == 0) { orig.fnptr!(fmv); return; }

        *(int*)(p + 0x6e0) = 0xfffe;
        *(int*)(p + 0x6d8) = 0xfffe;
        p[0x710] = 1;
        if (p[0x720] == 0) p[0x6d0] = 0;
        p[0x74c] = 0;

        FhUtil.set_at<byte>(EngineAddresses.GMovieSkipFlag, 1);

        _boot_skips++;
        _logger.Info($"[QoL] Boot video skipped (count={_boot_skips}, t={_clock.Elapsed.TotalSeconds:F1}s).");
    }

    /// <summary>True once the game is past the boot sequence, which is where these hooks stand down.</summary>
    private bool gameplay_ready()
    {
        int event_id = FhUtil.get_at<int>(EngineAddresses.EventId);
        if (event_id <= 0) return false;

        string name = event_name((uint)event_id);
        if (!is_title(event_id, name) && !is_splash((uint)event_id, name)) return true;

        return FhUtil.get_at<byte>(EngineAddresses.MenuState) != 0;
    }

    private static bool is_title(int event_id, string name)
        => event_id == TitleRoomId || string.Equals(name, "test20", StringComparison.OrdinalIgnoreCase);

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
